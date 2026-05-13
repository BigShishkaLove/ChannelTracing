namespace src.Application.Algorithms.Yoshimura;

public static class WeightedBipartiteMatcher
{
    public static List<MergeCandidate> FindMaximumWeightMatching(IReadOnlyCollection<MergeCandidate> candidates)
    {
        if (candidates.Count == 0)
            return new List<MergeCandidate>();

        var left = candidates.Select(c => c.Left).Distinct().OrderBy(g => g, CompositeNetComparer.Instance).ToList();
        var right = candidates.Select(c => c.Right).Distinct().OrderBy(g => g, CompositeNetComparer.Instance).ToList();
        var leftIndex = left.Select((group, index) => (group, index)).ToDictionary(x => x.group, x => x.index);
        var rightIndex = right.Select((group, index) => (group, index)).ToDictionary(x => x.group, x => x.index);
        var maxLongestPath = candidates.Max(c => c.LongestPathAfterMerge);

        var count = 1 + left.Count + right.Count + 1;
        var source = 0;
        var rightOffset = 1 + left.Count;
        var sink = count - 1;
        var graph = Enumerable.Range(0, count).Select(_ => new List<Edge>()).ToList();
        var edgeToCandidate = new Dictionary<(int From, int EdgeIndex), MergeCandidate>();

        for (var i = 0; i < left.Count; i++)
            AddEdge(graph, source, 1 + i, 1, 0);

        for (var i = 0; i < right.Count; i++)
            AddEdge(graph, rightOffset + i, sink, 1, 0);

        foreach (var candidate in candidates)
        {
            var from = 1 + leftIndex[candidate.Left];
            var to = rightOffset + rightIndex[candidate.Right];
            var edgeIndex = graph[from].Count;
            AddEdge(graph, from, to, 1, -CalculateWeight(candidate, maxLongestPath));
            edgeToCandidate[(from, edgeIndex)] = candidate;
        }

        while (TryFindShortestAugmentingPath(graph, source, sink, out var parentNode, out var parentEdge, out var pathCost) && pathCost < 0)
        {
            var current = sink;
            while (current != source)
            {
                var previous = parentNode[current];
                var edgeIndex = parentEdge[current];
                var edge = graph[previous][edgeIndex];
                edge.Capacity -= 1;
                graph[edge.To][edge.Reverse].Capacity += 1;
                current = previous;
            }
        }

        var selected = new List<MergeCandidate>();
        foreach (var entry in edgeToCandidate)
        {
            var from = entry.Key.From;
            var edgeIndex = entry.Key.EdgeIndex;
            var candidate = entry.Value;

            if (graph[from][edgeIndex].Capacity == 0)
                selected.Add(candidate);
        }

        selected.Sort(MergeCandidateComparer.Instance);
        return selected;
    }

    private static long CalculateWeight(MergeCandidate candidate, int maxLongestPath)
    {
        const long longestPathFactor = 1_000_000_000;
        const long zoneScoreFactor = 1_000_000;
        const long gapFactor = 1_000;

        var longestPathScore = maxLongestPath - candidate.LongestPathAfterMerge + 1L;
        var gapScore = Math.Max(0, 100_000 - candidate.Gap);
        var deterministicTieBreak = Math.Max(0, 10_000 - candidate.Left.PrimaryNetId) +
                                    Math.Max(0, 10_000 - candidate.Right.PrimaryNetId);

        return longestPathScore * longestPathFactor +
               candidate.SharedZoneBoundaryScore * zoneScoreFactor +
               gapScore * gapFactor +
               deterministicTieBreak;
    }

    private static bool TryFindShortestAugmentingPath(
        List<List<Edge>> graph,
        int source,
        int sink,
        out int[] parentNode,
        out int[] parentEdge,
        out long pathCost)
    {
        var distance = Enumerable.Repeat(long.MaxValue / 4, graph.Count).ToArray();
        parentNode = Enumerable.Repeat(-1, graph.Count).ToArray();
        parentEdge = Enumerable.Repeat(-1, graph.Count).ToArray();
        distance[source] = 0;

        for (var iteration = 0; iteration < graph.Count - 1; iteration++)
        {
            var changed = false;
            for (var from = 0; from < graph.Count; from++)
            {
                if (distance[from] == long.MaxValue / 4)
                    continue;

                for (var edgeIndex = 0; edgeIndex < graph[from].Count; edgeIndex++)
                {
                    var edge = graph[from][edgeIndex];
                    if (edge.Capacity <= 0)
                        continue;

                    var nextDistance = distance[from] + edge.Cost;
                    if (nextDistance >= distance[edge.To])
                        continue;

                    distance[edge.To] = nextDistance;
                    parentNode[edge.To] = from;
                    parentEdge[edge.To] = edgeIndex;
                    changed = true;
                }
            }

            if (!changed)
                break;
        }

        pathCost = distance[sink];
        return parentNode[sink] != -1;
    }

    private static void AddEdge(List<List<Edge>> graph, int from, int to, int capacity, long cost)
    {
        var forward = new Edge(to, graph[to].Count, capacity, cost);
        var reverse = new Edge(from, graph[from].Count, 0, -cost);
        graph[from].Add(forward);
        graph[to].Add(reverse);
    }

    private sealed class Edge
    {
        public Edge(int to, int reverse, int capacity, long cost)
        {
            To = to;
            Reverse = reverse;
            Capacity = capacity;
            Cost = cost;
        }

        public int To { get; }
        public int Reverse { get; }
        public int Capacity { get; set; }
        public long Cost { get; }
    }
}
