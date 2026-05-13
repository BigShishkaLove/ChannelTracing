using src.Application.Algorithms.Yoshimura;
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
        var nets = channel.Nets.Values
            .OrderBy(n => n.LeftmostColumn)
            .ThenBy(n => n.RightmostColumn)
            .ThenBy(n => n.Id)
            .ToList();

        var graph = VerticalConstraintGraph.Build(channel);
        var topoOrder = graph.GetTopologicalOrder(channel);

        if (topoOrder.Count != nets.Count)
        {
            var cycleNets = graph.NetIds.Except(topoOrder).ToList();
            var doglegPlan = graph.BreakCyclesWithDoglegs(channel);
            conflicts.Add(
                "VCG contains cyclic constraints involving nets: " +
                string.Join(", ", cycleNets) +
                ". Dogleg/split routing relaxed vertical constraints: " +
                string.Join(", ", doglegPlan.RelaxedEdges.Select(edge => $"{edge.From}->{edge.To}")) +
                ".");

            graph = doglegPlan.RepairedGraph;
        }

        var mergePlanner = MergePlanner.Create(channel, nets, graph);
        var compositeNets = mergePlanner.MergeCompatibleNets(nets);
        var tracksUsed = RouteCompositeNets(channel, compositeNets, graph, segments);

        DetectConflicts(segments, conflicts);
        return tracksUsed;
    }

    private static int RouteCompositeNets(
        Channel channel,
        List<CompositeNet> groups,
        VerticalConstraintGraph graph,
        List<Segment> segments)
    {
        var groupByNet = groups
            .SelectMany(group => group.NetIds.Select(netId => (netId, group)))
            .ToDictionary(pair => pair.netId, pair => pair.group);

        var successors = groups.ToDictionary(group => group, _ => new HashSet<CompositeNet>());
        var predecessors = groups.ToDictionary(group => group, _ => new HashSet<CompositeNet>());

        foreach (var edge in graph.Edges)
        {
            var from = groupByNet[edge.From];
            var to = groupByNet[edge.To];

            if (ReferenceEquals(from, to))
                continue;

            successors[from].Add(to);
            predecessors[to].Add(from);
        }

        var order = GetCompositeTopologicalOrder(groups, successors, predecessors);
        var trackIntervals = new Dictionary<int, List<(int start, int end)>>();
        var groupTrack = new Dictionary<CompositeNet, int>();

        foreach (var group in order)
        {
            var minTrack = predecessors[group]
                .Where(groupTrack.ContainsKey)
                .Select(pred => groupTrack[pred] + 1)
                .DefaultIfEmpty(0)
                .Max();

            var assignedTrack = minTrack;
            while (HasAnyHorizontalConflict(trackIntervals, assignedTrack, group.Intervals))
                assignedTrack++;

            groupTrack[group] = assignedTrack;

            if (!trackIntervals.ContainsKey(assignedTrack))
                trackIntervals[assignedTrack] = new List<(int, int)>();

            trackIntervals[assignedTrack].AddRange(group.Intervals);
        }

        var maxTrack = 0;
        foreach (var group in order)
        {
            var track = groupTrack[group];
            maxTrack = Math.Max(maxTrack, track + 1);

            foreach (var netId in group.NetIds.OrderBy(id => channel.Nets[id].LeftmostColumn).ThenBy(id => id))
                AddSegmentsForNet(channel.Nets[netId], track, segments);
        }

        return maxTrack;
    }

    private static List<CompositeNet> GetCompositeTopologicalOrder(
        List<CompositeNet> groups,
        Dictionary<CompositeNet, HashSet<CompositeNet>> successors,
        Dictionary<CompositeNet, HashSet<CompositeNet>> predecessors)
    {
        var inDegree = groups.ToDictionary(group => group, group => predecessors[group].Count);
        var ready = new SortedSet<CompositeNet>(CompositeNetComparer.Instance);

        foreach (var group in groups.Where(group => inDegree[group] == 0))
            ready.Add(group);

        var order = new List<CompositeNet>(groups.Count);
        while (ready.Count > 0)
        {
            var first = ready.Min!;
            ready.Remove(first);
            order.Add(first);

            foreach (var succ in successors[first].OrderBy(g => g, CompositeNetComparer.Instance))
            {
                if (--inDegree[succ] == 0)
                    ready.Add(succ);
            }
        }

        if (order.Count != groups.Count)
        {
            order.AddRange(groups
                .Except(order)
                .OrderBy(group => group, CompositeNetComparer.Instance));
        }

        return order;
    }

    private static int RouteIndividualNets(
        Channel channel,
        List<int> topoOrder,
        VerticalConstraintGraph graph,
        List<Segment> segments)
    {
        var netTrack = new Dictionary<int, int>(topoOrder.Count);
        var trackIntervals = new Dictionary<int, List<(int start, int end)>>();
        var maxTrack = 0;

        foreach (var netId in topoOrder)
        {
            var net = channel.Nets[netId];
            var minTrack = graph.Predecessors(netId)
                .Where(netTrack.ContainsKey)
                .Select(pred => netTrack[pred] + 1)
                .DefaultIfEmpty(0)
                .Max();

            var assignedTrack = minTrack;
            while (HasAnyHorizontalConflict(
                trackIntervals,
                assignedTrack,
                new List<(int start, int end)> { (net.LeftmostColumn, net.RightmostColumn) }))
            {
                assignedTrack++;
            }

            netTrack[netId] = assignedTrack;
            maxTrack = Math.Max(maxTrack, assignedTrack + 1);

            if (!trackIntervals.ContainsKey(assignedTrack))
                trackIntervals[assignedTrack] = new List<(int, int)>();
            trackIntervals[assignedTrack].Add((net.LeftmostColumn, net.RightmostColumn));

            AddSegmentsForNet(net, assignedTrack, segments);
        }

        return maxTrack;
    }

    private static bool HasAnyHorizontalConflict(
        Dictionary<int, List<(int start, int end)>> trackIntervals,
        int track,
        IReadOnlyCollection<(int start, int end)> intervals)
    {
        if (!trackIntervals.TryGetValue(track, out var occupied))
            return false;

        return intervals.Any(interval => HasHorizontalConflict(occupied, interval.start, interval.end));
    }

    private static bool HasHorizontalConflict(
        List<(int start, int end)> intervals,
        int left,
        int right)
        => intervals.Any(iv => iv.start <= right && iv.end >= left);

    private static void AddSegmentsForNet(Net net, int track, List<Segment> segments)
    {
        net.AssignedTrack = track;

        var hSeg = new Segment(
            net.Id,
            SegmentType.Horizontal,
            net.LeftmostColumn,
            net.RightmostColumn,
            track);
        segments.Add(hSeg);
        net.AddSegment(hSeg);

        CreateVerticalSegments(net, track, segments);
    }

    private static void CreateVerticalSegments(
        Net net,
        int track,
        List<Segment> segments)
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
