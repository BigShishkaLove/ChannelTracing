namespace src.Domain.Entities;

public class RoutingMetrics
{
    public int TracksUsed { get; set; }
    public double TotalWireLength { get; set; }
    public bool HasConflicts { get; set; }
    public int ConflictCount { get; set; }
    public double ExecutionTimeMs { get; set; }
    public string AlgorithmName { get; set; } = string.Empty;
}
