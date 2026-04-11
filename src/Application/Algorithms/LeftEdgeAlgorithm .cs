using src.Application.Interfaces;
using src.Domain.Entities;

namespace src.Application.Algorithms;

/// <summary>
/// Classic Left-Edge Algorithm for channel routing
/// Assigns nets to tracks based on their leftmost position
/// </summary>
public class LeftEdgeAlgorithm : RoutingAlgorithmBase
{
    public override string Name => "Left-Edge Algorithm";

    protected override int ExecuteRouting(Channel channel, List<Segment> segments, List<string> conflicts)
    {
        var nets = channel.Nets.Values.ToList();

        // Sort nets by their leftmost column (left-edge principle)
        var sortedNets = nets.OrderBy(n => n.LeftmostColumn)
                            .ThenBy(n => n.RightmostColumn)
                            .ToList();

        var tracks = new List<Track>();
        var maxTrack = 0;

        foreach (var net in sortedNets)
        {
            // Find the first available track for this net
            int assignedTrack = FindAvailableTrack(tracks, net);

            if (assignedTrack == -1)
            {
                // Need a new track
                assignedTrack = maxTrack++;
                tracks.Add(new Track(assignedTrack));
            }

            // Assign net to track
            net.AssignedTrack = assignedTrack;
            tracks[assignedTrack].AddNet(net);

            // Create horizontal segment for this net
            var horizontalSegment = new Segment(
                net.Id,
                SegmentType.Horizontal,
                net.LeftmostColumn,
                net.RightmostColumn,
                assignedTrack
            );
            segments.Add(horizontalSegment);
            net.AddSegment(horizontalSegment);

            // Create vertical segments (vias) to connect contacts
            CreateVerticalSegments(net, assignedTrack, segments);
        }

        // Detect any conflicts
        DetectConflicts(segments, conflicts);

        return maxTrack;
    }

    private int FindAvailableTrack(List<Track> tracks, Net net)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            if (!tracks[i].ConflictsWith(net))
            {
                return i;
            }
        }
        return -1; // No available track found
    }

    private void CreateVerticalSegments(Net net, int track, List<Segment> segments)
    {
        // Group contacts by column
        var contactsByColumn = net.Contacts.GroupBy(c => c.Column);

        foreach (var columnGroup in contactsByColumn)
        {
            var hasTop = columnGroup.Any(c => c.Position == ContactPosition.Top);
            var hasBottom = columnGroup.Any(c => c.Position == ContactPosition.Bottom);

            // Only create vertical segment if we have both top and bottom contacts
            if (hasTop && hasBottom)
            {
                var verticalSegment = new Segment(
                    net.Id,
                    SegmentType.Vertical,
                    columnGroup.Key,
                    columnGroup.Key,
                    track
                );
                segments.Add(verticalSegment);
                net.AddSegment(verticalSegment);
            }
        }
    }

    /// <summary>
    /// Internal class to represent a routing track
    /// </summary>
    private class Track
    {
        public int Id { get; }
        private readonly List<Net> _nets;
        private int _rightmostOccupiedColumn;

        public Track(int id)
        {
            Id = id;
            _nets = new List<Net>();
            _rightmostOccupiedColumn = -1;
        }

        public void AddNet(Net net)
        {
            _nets.Add(net);
            _rightmostOccupiedColumn = Math.Max(_rightmostOccupiedColumn, net.RightmostColumn);
        }

        public bool ConflictsWith(Net net)
        {
            // Net conflicts if its leftmost column overlaps with any existing net on this track
            return net.LeftmostColumn <= _rightmostOccupiedColumn;
        }
    }
}