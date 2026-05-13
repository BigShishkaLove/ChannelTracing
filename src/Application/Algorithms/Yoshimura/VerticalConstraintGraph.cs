using src.Domain.Entities;

namespace src.Application.Algorithms.Yoshimura;

public sealed class VerticalConstraintGraph
{
    private readonly Dictionary<int, HashSet<int>> _successors;
    private readonly Dictionary<int, HashSet<int>> _predecessors;
    private Dictionary<int, HashSet<int>>? _reachability;

    private VerticalConstraintGraph(
        IEnumerable<int> netIds,
        Dictionary<int, HashSet<int>> successors,
        Dictionary<int, HashSet<int>> predecessors)
    {
        NetIds = netIds.Order().ToList();
        _successors = successors;
        _predecessors = predecessors;
    }

    public IReadOnlyList<int> NetIds { get; }

    public IEnumerable<(int From, int To)> Edges => _successors
        .SelectMany(kvp => kvp.Value.Select(to => (From: kvp.Key, To: to)));

    public static VerticalConstraintGraph Build(Channel channel)
    {
        var netIds = channel.Nets.Keys.Order().ToList();
        var successors = netIds.ToDictionary(id => id, _ => new HashSet<int>());
        var predecessors = netIds.ToDictionary(id => id, _ => new HashSet<int>());

        for (var col = 0; col < channel.Width; col++)
        {
            var topNet = channel.TopRow[col];
            var bottomNet = channel.BottomRow[col];

            if (topNet == 0 || bottomNet == 0 || topNet == bottomNet)
                continue;

            successors[topNet].Add(bottomNet);
            predecessors[bottomNet].Add(topNet);
        }

        return new VerticalConstraintGraph(netIds, successors, predecessors);
    }

    public static VerticalConstraintGraph FromEdges(
        IEnumerable<int> netIds,
        IEnumerable<(int From, int To)> edges)
    {
        var orderedNetIds = netIds.Order().ToList();
        var successors = orderedNetIds.ToDictionary(id => id, _ => new HashSet<int>());
        var predecessors = orderedNetIds.ToDictionary(id => id, _ => new HashSet<int>());

        foreach (var edge in edges)
        {
            if (edge.From == edge.To)
                continue;

            successors[edge.From].Add(edge.To);
            predecessors[edge.To].Add(edge.From);
        }

        return new VerticalConstraintGraph(orderedNetIds, successors, predecessors);
    }

    public IReadOnlyCollection<int> Predecessors(int netId) => _predecessors[netId];

    public List<int> GetTopologicalOrder(Channel channel)
    {
        var inDegree = NetIds.ToDictionary(id => id, id => _predecessors[id].Count);
        var ready = new SortedSet<(int leftCol, int id)>(
            Comparer<(int leftCol, int id)>.Create((a, b) =>
                a.leftCol != b.leftCol
                    ? a.leftCol.CompareTo(b.leftCol)
                    : a.id.CompareTo(b.id)));

        foreach (var id in NetIds.Where(id => inDegree[id] == 0))
            ready.Add((channel.Nets[id].LeftmostColumn, id));

        var order = new List<int>(NetIds.Count);
        while (ready.Count > 0)
        {
            var first = ready.Min;
            ready.Remove(first);
            order.Add(first.id);

            foreach (var succ in _successors[first.id].Order())
            {
                if (--inDegree[succ] == 0)
                    ready.Add((channel.Nets[succ].LeftmostColumn, succ));
            }
        }

        return order;
    }

    public DoglegSplitPlan BreakCyclesWithDoglegs(Channel channel)
    {
        var activeEdges = Edges.OrderBy(edge => edge.From).ThenBy(edge => edge.To).ToHashSet();
        var relaxed = new List<(int From, int To)>();

        while (true)
        {
            var graph = FromEdges(NetIds, activeEdges);
            var order = graph.GetTopologicalOrder(channel);
            if (order.Count == NetIds.Count)
                return new DoglegSplitPlan(graph, relaxed);

            var unresolved = NetIds.Except(order).ToHashSet();
            var edgeToRelax = activeEdges
                .Where(edge => unresolved.Contains(edge.From) && unresolved.Contains(edge.To))
                .OrderByDescending(edge => channel.Nets[edge.From].RightmostColumn - channel.Nets[edge.From].LeftmostColumn)
                .ThenBy(edge => edge.From)
                .ThenBy(edge => edge.To)
                .FirstOrDefault();

            if (edgeToRelax == default)
                return new DoglegSplitPlan(graph, relaxed);

            activeEdges.Remove(edgeToRelax);
            relaxed.Add(edgeToRelax);
        }
    }

    public bool HasPath(int from, int to)
    {
        _reachability ??= BuildReachability();
        return _reachability[from].Contains(to);
    }

    public VerticalConstraintGraph UpdateAfterMerge(IReadOnlyCollection<CompositeNet> groups)
    {
        var componentByNet = BuildComponentMap(groups);
        return BuildComponentGraph(componentByNet);
    }

    public int LongestPathAfterMerge(
        IReadOnlyCollection<CompositeNet> groups,
        CompositeNet left,
        CompositeNet right)
    {
        var componentByNet = BuildComponentMap(groups);
        var mergedComponent = Math.Min(left.PrimaryNetId, right.PrimaryNetId);

        foreach (var netId in left.NetIds.Concat(right.NetIds))
            componentByNet[netId] = mergedComponent;

        var componentGraph = BuildComponentGraph(componentByNet);
        return componentGraph.LongestPath();
    }

    public int LongestPath()
        => LongestPathInDag(NetIds.ToList(), _successors, _predecessors);

    private Dictionary<int, int> BuildComponentMap(IReadOnlyCollection<CompositeNet> groups)
    {
        var componentByNet = NetIds.ToDictionary(id => id, id => id);

        foreach (var group in groups)
        {
            foreach (var netId in group.NetIds)
                componentByNet[netId] = group.PrimaryNetId;
        }

        return componentByNet;
    }

    private VerticalConstraintGraph BuildComponentGraph(Dictionary<int, int> componentByNet)
    {
        var components = componentByNet.Values.Distinct().Order().ToList();
        var successors = components.ToDictionary(id => id, _ => new HashSet<int>());
        var predecessors = components.ToDictionary(id => id, _ => new HashSet<int>());

        foreach (var edge in Edges)
        {
            var from = componentByNet[edge.From];
            var to = componentByNet[edge.To];

            if (from == to)
                continue;

            successors[from].Add(to);
            predecessors[to].Add(from);
        }

        return new VerticalConstraintGraph(components, successors, predecessors);
    }

    private Dictionary<int, HashSet<int>> BuildReachability()
    {
        var reachability = NetIds.ToDictionary(id => id, _ => new HashSet<int>());

        foreach (var source in NetIds)
        {
            var stack = new Stack<int>(_successors[source]);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!reachability[source].Add(current))
                    continue;

                foreach (var succ in _successors[current])
                    stack.Push(succ);
            }
        }

        return reachability;
    }

    private static int LongestPathInDag(
        List<int> nodes,
        Dictionary<int, HashSet<int>> successors,
        Dictionary<int, HashSet<int>> predecessors)
    {
        var inDegree = nodes.ToDictionary(id => id, id => predecessors[id].Count);
        var ready = new SortedSet<int>(nodes.Where(id => inDegree[id] == 0));
        var distance = nodes.ToDictionary(id => id, _ => 1);
        var visited = 0;
        var longest = nodes.Count == 0 ? 0 : 1;

        while (ready.Count > 0)
        {
            var current = ready.Min;
            ready.Remove(current);
            visited++;
            longest = Math.Max(longest, distance[current]);

            foreach (var succ in successors[current].Order())
            {
                distance[succ] = Math.Max(distance[succ], distance[current] + 1);
                if (--inDegree[succ] == 0)
                    ready.Add(succ);
            }
        }

        return visited == nodes.Count ? longest : int.MaxValue;
    }
}
