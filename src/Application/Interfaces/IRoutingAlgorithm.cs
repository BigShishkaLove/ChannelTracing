using src.Domain.Entities;

namespace src.Application.Interfaces;

/// <summary>
/// Interface for channel routing algorithms (Strategy pattern)
/// </summary>
public interface IRoutingAlgorithm
{
    string Name { get; }
    RoutingResult Route(Channel channel);
}

/// <summary>
/// Base class for routing algorithms with common functionality
/// </summary>
public abstract class RoutingAlgorithmBase : IRoutingAlgorithm
{
    public abstract string Name { get; }

    public RoutingResult Route(Channel channel)
    {
        var startTime = DateTime.Now;

        var segments = new List<Segment>();
        var conflicts = new List<string>();
        var tracksUsed = 0;

        try
        {
            tracksUsed = ExecuteRouting(channel, segments, conflicts);
        }
        catch (Exception ex)
        {
            conflicts.Add($"Algorithm error: {ex.Message}");
        }

        var executionTime = DateTime.Now - startTime;

        return new RoutingResult(
            channel,
            tracksUsed,
            segments,
            conflicts.Count > 0,
            conflicts,
            executionTime,
            Name
        );
    }

    protected abstract int ExecuteRouting(Channel channel, List<Segment> segments, List<string> conflicts);

    /// <summary>
    /// Checks if two horizontal segments on the same track overlap
    /// </summary>
    protected bool SegmentsOverlap(Segment seg1, Segment seg2)
    {
        if (seg1.Track != seg2.Track || seg1.Type != SegmentType.Horizontal || seg2.Type != SegmentType.Horizontal)
            return false;

        return !(seg1.EndColumn < seg2.StartColumn || seg2.EndColumn < seg1.StartColumn);
    }

    /// <summary>
    /// Detects conflicts (overlapping segments on the same track)
    /// </summary>
    protected void DetectConflicts(List<Segment> segments, List<string> conflicts)
    {
        var horizontalSegments = segments.Where(s => s.Type == SegmentType.Horizontal).ToList();

        for (int i = 0; i < horizontalSegments.Count; i++)
        {
            for (int j = i + 1; j < horizontalSegments.Count; j++)
            {
                if (SegmentsOverlap(horizontalSegments[i], horizontalSegments[j]))
                {
                    conflicts.Add(
                        $"Conflict: Net {horizontalSegments[i].NetId} and Net {horizontalSegments[j].NetId} " +
                        $"overlap on track {horizontalSegments[i].Track}"
                    );
                }
            }
        }
    }
}