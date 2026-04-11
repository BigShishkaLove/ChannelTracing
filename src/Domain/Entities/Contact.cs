namespace src.Domain.Entities;

/// <summary>
/// Position of a contact in the channel
/// </summary>
public enum ContactPosition
{
    Top,
    Bottom
}

/// <summary>
/// Type of routing segment
/// </summary>
public enum SegmentType
{
    Horizontal,  // Track segment
    Vertical     // Via/connection between top and bottom
}

/// <summary>
/// Represents a contact point in the channel
/// </summary>
public class Contact
{
    public int Column { get; }
    public ContactPosition Position { get; }
    public int NetId { get; }

    public Contact(int column, ContactPosition position, int netId)
    {
        if (column < 0)
            throw new ArgumentException("Column must be non-negative", nameof(column));

        if (netId <= 0)
            throw new ArgumentException("Net ID must be positive", nameof(netId));

        Column = column;
        Position = position;
        NetId = netId;
    }

    public override string ToString() => $"Contact(Net={NetId}, Col={Column}, Pos={Position})";
}

/// <summary>
/// Represents a routing segment (horizontal track or vertical via)
/// </summary>
public class Segment
{
    public int NetId { get; }
    public SegmentType Type { get; }
    public int StartColumn { get; }
    public int EndColumn { get; }
    public int Track { get; }

    public Segment(int netId, SegmentType type, int startColumn, int endColumn, int track)
    {
        if (netId <= 0)
            throw new ArgumentException("Net ID must be positive", nameof(netId));

        if (startColumn < 0 || endColumn < 0)
            throw new ArgumentException("Columns must be non-negative");

        if (startColumn > endColumn && type == SegmentType.Horizontal)
            throw new ArgumentException("Start column must be <= end column for horizontal segments");

        if (track < 0)
            throw new ArgumentException("Track must be non-negative", nameof(track));

        NetId = netId;
        Type = type;
        StartColumn = startColumn;
        EndColumn = endColumn;
        Track = track;
    }

    public int Length => Math.Abs(EndColumn - StartColumn);

    public override string ToString() =>
        $"Segment(Net={NetId}, Type={Type}, Cols=[{StartColumn}-{EndColumn}], Track={Track})";
}