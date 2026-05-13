namespace src.Application.Algorithms.Yoshimura;

public sealed class HorizontalNonConstraintGraph
{
    private readonly HashSet<(int first, int second)> _compatiblePairs;

    private HorizontalNonConstraintGraph(HashSet<(int first, int second)> compatiblePairs)
        => _compatiblePairs = compatiblePairs;

    public static HorizontalNonConstraintGraph Build(
        IReadOnlyCollection<CompositeNet> groups,
        VerticalConstraintGraph verticalGraph,
        HorizontalConstraintGraph horizontalGraph)
    {
        var compatiblePairs = new HashSet<(int first, int second)>();
        var ordered = groups.OrderBy(g => g.PrimaryNetId).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (CanMerge(ordered[i], ordered[j], verticalGraph, horizontalGraph))
                    compatiblePairs.Add(Normalize(ordered[i].PrimaryNetId, ordered[j].PrimaryNetId));
            }
        }

        return new HorizontalNonConstraintGraph(compatiblePairs);
    }

    public HorizontalNonConstraintGraph UpdateAfterMerge(
        IReadOnlyCollection<CompositeNet> groups,
        VerticalConstraintGraph verticalGraph,
        HorizontalConstraintGraph horizontalGraph)
        => Build(groups, verticalGraph, horizontalGraph);

    public bool AreCompatible(CompositeNet first, CompositeNet second)
        => _compatiblePairs.Contains(Normalize(first.PrimaryNetId, second.PrimaryNetId));

    private static bool CanMerge(
        CompositeNet first,
        CompositeNet second,
        VerticalConstraintGraph verticalGraph,
        HorizontalConstraintGraph horizontalGraph)
    {
        if (horizontalGraph.Conflicts(first, second))
            return false;

        foreach (var firstNet in first.NetIds)
        {
            foreach (var secondNet in second.NetIds)
            {
                if (verticalGraph.HasPath(firstNet, secondNet) || verticalGraph.HasPath(secondNet, firstNet))
                    return false;
            }
        }

        return true;
    }

    private static (int first, int second) Normalize(int first, int second)
        => first < second ? (first, second) : (second, first);
}
