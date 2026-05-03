namespace src.Domain.Entities;

public class Segment
{
    public int NetId { get; }
    public SegmentType Type { get; }
    public int StartColumn { get; }
    public int EndColumn { get; }
    public int Track { get; }

    public Segment(int netId, SegmentType type, int startColumn, int endColumn, int track)
    {
        if (netId <= 0) throw new ArgumentException("Net ID must be positive", nameof(netId));
        if (startColumn < 0 || endColumn < 0) throw new ArgumentException("Columns must be non-negative");
        if (startColumn > endColumn && type == SegmentType.Horizontal)
            throw new ArgumentException("Start column must be <= end column for horizontal segments");
        if (track < 0) throw new ArgumentException("Track must be non-negative", nameof(track));

        NetId = netId;
        Type = type;
        StartColumn = startColumn;
        EndColumn = endColumn;
        Track = track;
    }

    public int Length => Math.Abs(EndColumn - StartColumn);
}
