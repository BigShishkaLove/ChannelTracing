using src.Domain.Entities;

namespace src.Application.Algorithms.Yoshimura;

public sealed class HorizontalConstraintGraph
{
    private readonly HashSet<(int first, int second)> _conflicts;

    private HorizontalConstraintGraph(HashSet<(int first, int second)> conflicts)
        => _conflicts = conflicts;

    public static HorizontalConstraintGraph Build(IEnumerable<Net> nets)
    {
        var ordered = nets.OrderBy(n => n.LeftmostColumn).ThenBy(n => n.RightmostColumn).ThenBy(n => n.Id).ToList();
        var active = new List<Net>();
        var conflicts = new HashSet<(int first, int second)>();

        foreach (var net in ordered)
        {
            active.RemoveAll(other => other.RightmostColumn < net.LeftmostColumn);

            foreach (var other in active)
                conflicts.Add(Normalize(other.Id, net.Id));

            active.Add(net);
        }

        return new HorizontalConstraintGraph(conflicts);
    }

    public HorizontalConstraintGraph UpdateAfterMerge(IReadOnlyCollection<CompositeNet> groups)
    {
        var conflicts = new HashSet<(int first, int second)>();
        var ordered = groups.OrderBy(g => g.LeftmostColumn).ThenBy(g => g.RightmostColumn).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (ordered[j].LeftmostColumn > ordered[i].RightmostColumn)
                    break;

                if (CompositeIntervalsOverlap(ordered[i], ordered[j]))
                    conflicts.Add(Normalize(ordered[i].PrimaryNetId, ordered[j].PrimaryNetId));
            }
        }

        return new HorizontalConstraintGraph(conflicts);
    }

    public bool Conflicts(int first, int second)
        => _conflicts.Contains(Normalize(first, second));

    public bool Conflicts(CompositeNet first, CompositeNet second)
        => _conflicts.Contains(Normalize(first.PrimaryNetId, second.PrimaryNetId)) ||
           CompositeIntervalsOverlap(first, second);

    private static bool CompositeIntervalsOverlap(CompositeNet first, CompositeNet second)
    {
        foreach (var left in first.Intervals)
        {
            foreach (var right in second.Intervals)
            {
                if (left.start <= right.end && right.start <= left.end)
                    return true;
            }
        }

        return false;
    }

    private static (int first, int second) Normalize(int first, int second)
        => first < second ? (first, second) : (second, first);
}
