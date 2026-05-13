namespace src.Application.Algorithms.Yoshimura;

public sealed class YoshimuraZoneScanner
{
    private readonly VerticalConstraintGraph _verticalGraph;
    private readonly HorizontalNonConstraintGraph _horizontalNonConstraintGraph;
    private readonly ZoneTable _zoneTable;

    public YoshimuraZoneScanner(
        VerticalConstraintGraph verticalGraph,
        HorizontalNonConstraintGraph horizontalNonConstraintGraph,
        ZoneTable zoneTable)
    {
        _verticalGraph = verticalGraph;
        _horizontalNonConstraintGraph = horizontalNonConstraintGraph;
        _zoneTable = zoneTable;
    }

    public List<MergeCandidate> SelectMergeBatch(List<CompositeNet> groups)
    {
        var selected = new List<MergeCandidate>();
        var used = new HashSet<CompositeNet>();

        foreach (var boundary in _zoneTable.GetBoundaryCandidateSets(groups))
        {
            var localCandidates = BuildLocalCandidates(boundary, groups, used);
            var localMatching = WeightedBipartiteMatcher.FindMaximumWeightMatching(localCandidates);

            foreach (var candidate in localMatching)
            {
                if (used.Contains(candidate.Left) || used.Contains(candidate.Right))
                    continue;

                used.Add(candidate.Left);
                used.Add(candidate.Right);
                selected.Add(candidate);
            }
        }

        selected.Sort(MergeCandidateComparer.Instance);
        return selected;
    }

    private List<MergeCandidate> BuildLocalCandidates(
        ZoneBoundaryCandidateSet boundary,
        IReadOnlyCollection<CompositeNet> groups,
        HashSet<CompositeNet> used)
    {
        var candidates = new List<MergeCandidate>();

        foreach (var left in boundary.EndingGroups)
        {
            if (used.Contains(left))
                continue;

            foreach (var right in boundary.StartingGroups)
            {
                if (used.Contains(right) || ReferenceEquals(left, right))
                    continue;

                var ordered = left.LeftmostColumn <= right.LeftmostColumn
                    ? (Left: left, Right: right)
                    : (Left: right, Right: left);

                if (!_horizontalNonConstraintGraph.AreCompatible(ordered.Left, ordered.Right))
                    continue;

                candidates.Add(MergeCandidate.Create(ordered.Left, ordered.Right, groups, _verticalGraph, _zoneTable));
            }
        }

        return candidates;
    }
}
