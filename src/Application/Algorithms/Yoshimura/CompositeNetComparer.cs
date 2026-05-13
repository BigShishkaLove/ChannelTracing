namespace src.Application.Algorithms.Yoshimura;

public sealed class CompositeNetComparer : IComparer<CompositeNet>
{
    public static readonly CompositeNetComparer Instance = new();

    public int Compare(CompositeNet? x, CompositeNet? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        var byLeft = x.LeftmostColumn.CompareTo(y.LeftmostColumn);
        if (byLeft != 0)
            return byLeft;

        var byRight = x.RightmostColumn.CompareTo(y.RightmostColumn);
        if (byRight != 0)
            return byRight;

        return x.PrimaryNetId.CompareTo(y.PrimaryNetId);
    }
}
