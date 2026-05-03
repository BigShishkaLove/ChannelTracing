using src.Domain.Entities;

namespace src.Application.Interfaces;

public abstract class RoutingAlgorithmBase : IRoutingAlgorithm
{
    public abstract string Name { get; }

    public RoutingResult Route(Channel channel)
    {
        var startTime = DateTime.UtcNow;
        var segments = new List<Segment>();
        var conflicts = new List<string>();

        var tracksUsed = ExecuteRouting(channel, segments, conflicts);

        return new RoutingResult(
            channel,
            tracksUsed,
            segments,
            conflicts.Count > 0,
            conflicts,
            DateTime.UtcNow - startTime,
            Name);
    }

    protected abstract int ExecuteRouting(Channel channel, List<Segment> segments, List<string> conflicts);

    protected static void DetectConflicts(List<Segment> segments, List<string> conflicts)
    {
        var horizontal = segments.Where(s => s.Type == SegmentType.Horizontal)
            .OrderBy(s => s.Track).ThenBy(s => s.StartColumn).ToList();

        for (var i = 1; i < horizontal.Count; i++)
        {
            var prev = horizontal[i - 1];
            var cur = horizontal[i];
            if (prev.Track == cur.Track && prev.EndColumn >= cur.StartColumn)
            {
                conflicts.Add($"Conflict: Net {prev.NetId} and Net {cur.NetId} overlap on track {cur.Track}");
            }
        }
    }
}
