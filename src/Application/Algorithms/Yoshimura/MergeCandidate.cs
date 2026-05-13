namespace src.Application.Algorithms.Yoshimura;

public sealed record MergeCandidate(
    CompositeNet Left,
    CompositeNet Right,
    int LongestPathAfterMerge,
    int SharedZoneBoundaryScore,
    int Gap)
{
    public static MergeCandidate Create(
        CompositeNet left,
        CompositeNet right,
        IReadOnlyCollection<CompositeNet> groups,
        VerticalConstraintGraph graph,
        ZoneTable zones)
    {
        var ordered = left.LeftmostColumn <= right.LeftmostColumn
            ? (Left: left, Right: right)
            : (Left: right, Right: left);
        var gap = Math.Max(0, ordered.Right.LeftmostColumn - ordered.Left.RightmostColumn - 1);

        return new MergeCandidate(
            ordered.Left,
            ordered.Right,
            graph.LongestPathAfterMerge(groups, ordered.Left, ordered.Right),
            zones.SharedBoundaryScore(ordered.Left, ordered.Right),
            gap);
    }
}
