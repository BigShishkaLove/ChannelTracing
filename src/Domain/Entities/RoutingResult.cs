namespace src.Domain.Entities;

/// <summary>
/// Contains the result of a routing algorithm execution
/// </summary>
public class RoutingResult
{
    public Channel Channel { get; }
    public int TracksUsed { get; }
    public List<Segment> AllSegments { get; }
    public bool HasConflicts { get; }
    public List<string> ConflictDescriptions { get; }
    public TimeSpan ExecutionTime { get; }
    public string AlgorithmName { get; }

    public RoutingResult(
        Channel channel,
        int tracksUsed,
        List<Segment> segments,
        bool hasConflicts,
        List<string> conflictDescriptions,
        TimeSpan executionTime,
        string algorithmName)
    {
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        TracksUsed = tracksUsed;
        AllSegments = segments ?? new List<Segment>();
        HasConflicts = hasConflicts;
        ConflictDescriptions = conflictDescriptions ?? new List<string>();
        ExecutionTime = executionTime;
        AlgorithmName = algorithmName ?? "Unknown";
    }

    public double TotalWireLength => AllSegments.Sum(s => s.Length);

    public RoutingMetrics GetMetrics() => new RoutingMetrics
    {
        TracksUsed = TracksUsed,
        TotalWireLength = TotalWireLength,
        HasConflicts = HasConflicts,
        ConflictCount = ConflictDescriptions.Count,
        ExecutionTimeMs = ExecutionTime.TotalMilliseconds,
        AlgorithmName = AlgorithmName
    };

    public override string ToString() =>
        $"Routing Result ({AlgorithmName}):\n" +
        $"  Tracks Used: {TracksUsed}\n" +
        $"  Wire Length: {TotalWireLength}\n" +
        $"  Conflicts: {(HasConflicts ? $"Yes ({ConflictDescriptions.Count})" : "No")}\n" +
        $"  Execution Time: {ExecutionTime.TotalMilliseconds:F2} ms";
}

/// <summary>
/// Metrics for comparing routing algorithms
/// </summary>
public class RoutingMetrics
{
    public int TracksUsed { get; set; }
    public double TotalWireLength { get; set; }
    public bool HasConflicts { get; set; }
    public int ConflictCount { get; set; }
    public double ExecutionTimeMs { get; set; }
    public string AlgorithmName { get; set; } = string.Empty;

    public override string ToString() =>
        $"{AlgorithmName,-20} | Tracks: {TracksUsed,3} | Wire: {TotalWireLength,6:F0} | " +
        $"Conflicts: {ConflictCount,3} | Time: {ExecutionTimeMs,7:F2} ms";
}