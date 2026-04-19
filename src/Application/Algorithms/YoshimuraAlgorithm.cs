using src.Application.Interfaces;
using src.Domain.Entities;

namespace src.Application.Algorithms;

public class YoshimuraAlgorithm : RoutingAlgorithmBase
{
    public override string Name => "Yoshimura Algorithm";

    protected override int ExecuteRouting(
        Channel channel,
        List<Segment> segments,
        List<string> conflicts)
    {
        var nets   = channel.Nets.Values.ToList();
        var netIds = nets.Select(n => n.Id).ToList();

        var successors   = netIds.ToDictionary(id => id, _ => new HashSet<int>());
        var predecessors = netIds.ToDictionary(id => id, _ => new HashSet<int>());

        for (int col = 0; col < channel.Width; col++)
        {
            int topNet = channel.TopRow[col];
            int botNet = channel.BottomRow[col];

            if (topNet != 0 && botNet != 0 && topNet != botNet)
            {
                successors[topNet].Add(botNet);
                predecessors[botNet].Add(topNet);
            }
        }

        var inDegree = netIds.ToDictionary(id => id, id => predecessors[id].Count);

        var ready = new SortedSet<(int leftCol, int id)>(
            Comparer<(int leftCol, int id)>.Create((a, b) =>
                a.leftCol != b.leftCol
                    ? a.leftCol.CompareTo(b.leftCol)
                    : a.id.CompareTo(b.id)));

        foreach (var id in netIds.Where(id => inDegree[id] == 0))
            ready.Add((channel.Nets[id].LeftmostColumn, id));

        var topoOrder = new List<int>(netIds.Count);

        while (ready.Count > 0)
        {
            var first = ready.Min;
            ready.Remove(first);
            topoOrder.Add(first.id);

            foreach (var succ in successors[first.id])
            {
                if (--inDegree[succ] == 0)
                    ready.Add((channel.Nets[succ].LeftmostColumn, succ));
            }
        }

        var cycleNets = netIds.Except(topoOrder).ToList();
        if (cycleNets.Count > 0)
        {
            conflicts.Add(
                "VCG contains cyclic constraints involving nets: " +
                string.Join(", ", cycleNets) +
                ". These nets cannot satisfy all vertical ordering " +
                "requirements simultaneously and are routed greedily.");

            topoOrder.AddRange(cycleNets.OrderBy(id => channel.Nets[id].LeftmostColumn));
        }

        var netTrack = new Dictionary<int, int>(netIds.Count);
        var trackIntervals = new Dictionary<int, List<(int start, int end)>>();

        foreach (var netId in topoOrder)
        {
            var net = channel.Nets[netId];

            int minTrack = predecessors[netId]
                .Where(p => netTrack.ContainsKey(p))
                .Select(p => netTrack[p] + 1)
                .DefaultIfEmpty(0)
                .Max();

            int assignedTrack = minTrack;
            while (true)
            {
                if (!trackIntervals.TryGetValue(assignedTrack, out var intervals) ||
                    !HasHorizontalConflict(intervals, net.LeftmostColumn, net.RightmostColumn))
                    break;

                assignedTrack++;
            }

            netTrack[netId] = assignedTrack;

            if (!trackIntervals.ContainsKey(assignedTrack))
                trackIntervals[assignedTrack] = new List<(int, int)>();
            trackIntervals[assignedTrack].Add((net.LeftmostColumn, net.RightmostColumn));
        }

        int maxTrack = 0;

        foreach (var netId in topoOrder)
        {
            var net   = channel.Nets[netId];
            int track = netTrack[netId];
            net.AssignedTrack = track;

            if (track + 1 > maxTrack)
                maxTrack = track + 1;

            var hSeg = new Segment(
                netId, SegmentType.Horizontal,
                net.LeftmostColumn, net.RightmostColumn, track);
            segments.Add(hSeg);
            net.AddSegment(hSeg);

            CreateVerticalSegments(net, track, segments);
        }

        DetectConflicts(segments, conflicts);
        return maxTrack;
    }

    private static bool HasHorizontalConflict(
        List<(int start, int end)> intervals,
        int left,
        int right)
        => intervals.Any(iv => iv.start <= right && iv.end >= left);

    private static void CreateVerticalSegments(
        Net net, int track, List<Segment> segments)
    {
        foreach (var colGroup in net.Contacts.GroupBy(c => c.Column))
        {
            bool hasTop    = colGroup.Any(c => c.Position == ContactPosition.Top);
            bool hasBottom = colGroup.Any(c => c.Position == ContactPosition.Bottom);

            if (hasTop && hasBottom)
            {
                var vSeg = new Segment(
                    net.Id, SegmentType.Vertical,
                    colGroup.Key, colGroup.Key, track);
                segments.Add(vSeg);
                net.AddSegment(vSeg);
            }
        }
    }
}