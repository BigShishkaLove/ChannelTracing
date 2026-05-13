using src.Domain.Entities;

namespace src.Application.Algorithms.Yoshimura;

public sealed class CompositeNet
{
    private readonly HashSet<int> _startColumns;
    private readonly HashSet<int> _endColumns;

    public CompositeNet(Net net)
    {
        NetIds = new List<int> { net.Id };
        Intervals = new List<(int start, int end)> { (net.LeftmostColumn, net.RightmostColumn) };
        LeftmostColumn = net.LeftmostColumn;
        RightmostColumn = net.RightmostColumn;
        PrimaryNetId = net.Id;
        _startColumns = new HashSet<int> { net.LeftmostColumn };
        _endColumns = new HashSet<int> { net.RightmostColumn };
    }

    private CompositeNet(List<int> netIds, List<(int start, int end)> intervals)
    {
        NetIds = netIds;
        Intervals = intervals;
        LeftmostColumn = intervals.Min(iv => iv.start);
        RightmostColumn = intervals.Max(iv => iv.end);
        PrimaryNetId = netIds.Min();
        _startColumns = intervals.Select(iv => iv.start).ToHashSet();
        _endColumns = intervals.Select(iv => iv.end).ToHashSet();
    }

    public List<int> NetIds { get; }
    public List<(int start, int end)> Intervals { get; }
    public int LeftmostColumn { get; }
    public int RightmostColumn { get; }
    public int PrimaryNetId { get; }

    public bool StartsAt(int column) => _startColumns.Contains(column);

    public bool EndsAt(int column) => _endColumns.Contains(column);

    public bool ContainsNet(int netId) => NetIds.Contains(netId);

    public CompositeNet Merge(CompositeNet other)
        => new(
            NetIds.Concat(other.NetIds).Order().ToList(),
            Intervals.Concat(other.Intervals)
                .OrderBy(iv => iv.start)
                .ThenBy(iv => iv.end)
                .ToList());
}
