namespace src.Application.Algorithms.Yoshimura;

public sealed class MergeCandidateComparer : IComparer<MergeCandidate>
{
    public static readonly MergeCandidateComparer Instance = new();

    public int Compare(MergeCandidate? x, MergeCandidate? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        var byLongestPath = x.LongestPathAfterMerge.CompareTo(y.LongestPathAfterMerge);
        if (byLongestPath != 0)
            return byLongestPath;

        var byZoneScore = y.SharedZoneBoundaryScore.CompareTo(x.SharedZoneBoundaryScore);
        if (byZoneScore != 0)
            return byZoneScore;

        var byGap = x.Gap.CompareTo(y.Gap);
        if (byGap != 0)
            return byGap;

        var byLeft = x.Left.LeftmostColumn.CompareTo(y.Left.LeftmostColumn);
        if (byLeft != 0)
            return byLeft;

        var byRight = x.Right.LeftmostColumn.CompareTo(y.Right.LeftmostColumn);
        if (byRight != 0)
            return byRight;

        var byLeftId = x.Left.PrimaryNetId.CompareTo(y.Left.PrimaryNetId);
        return byLeftId != 0
            ? byLeftId
            : x.Right.PrimaryNetId.CompareTo(y.Right.PrimaryNetId);
    }
}
