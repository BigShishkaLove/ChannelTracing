namespace src.Domain.Entities;

public class RoutingResult
{
    public Channel Channel { get; }
    public int TracksUsed { get; }
    public List<Segment> AllSegments { get; }
    public bool HasConflicts { get; }
    public List<string> ConflictDescriptions { get; }
    public TimeSpan ExecutionTime { get; }
    public string AlgorithmName { get; }

    public RoutingResult(Channel channel, int tracksUsed, List<Segment> segments, bool hasConflicts, List<string> conflictDescriptions, TimeSpan executionTime, string algorithmName)
    {
        Channel = channel;
        TracksUsed = tracksUsed;
        AllSegments = segments;
        HasConflicts = hasConflicts;
        ConflictDescriptions = conflictDescriptions;
        ExecutionTime = executionTime;
        AlgorithmName = algorithmName;
    }

    public double TotalWireLength => AllSegments.Sum(s => s.Length);

    public RoutingMetrics GetMetrics() => new()
    {
        TracksUsed = TracksUsed,
        TotalWireLength = TotalWireLength,
        HasConflicts = HasConflicts,
        ConflictCount = ConflictDescriptions.Count,
        ExecutionTimeMs = ExecutionTime.TotalMilliseconds,
        AlgorithmName = AlgorithmName
    };
}
