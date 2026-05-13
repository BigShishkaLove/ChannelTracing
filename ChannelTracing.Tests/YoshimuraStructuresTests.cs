using src.Application.Algorithms.Yoshimura;
using src.Domain.Entities;

namespace ChannelTracing.Tests;

public class YoshimuraStructuresTests
{
    [Fact]
    public void VerticalConstraintGraph_BuildsReachabilityAndLongestPath()
    {
        var channel = new Channel(
            4,
            topRow:    new[] { 1, 2, 3, 0 },
            bottomRow: new[] { 2, 3, 4, 1 });

        var graph = VerticalConstraintGraph.Build(channel);
        var order = graph.GetTopologicalOrder(channel);

        Assert.Equal(new[] { 1, 2, 3, 4 }, order);
        Assert.True(graph.HasPath(1, 4));
        Assert.False(graph.HasPath(4, 1));
        Assert.Equal(4, graph.LongestPath());
    }

    [Fact]
    public void HorizontalGraphs_ExposeConflictsAndNonConstraintsForMergeCandidates()
    {
        var channel = new Channel(
            6,
            topRow:    new[] { 1, 2, 0, 3, 0, 0 },
            bottomRow: new[] { 0, 1, 2, 0, 3, 4 });
        var groups = channel.Nets.Values.Select(net => new CompositeNet(net)).ToList();
        var verticalGraph = VerticalConstraintGraph.Build(channel);
        var horizontalGraph = HorizontalConstraintGraph.Build(channel.Nets.Values);
        var nonConstraintGraph = HorizontalNonConstraintGraph.Build(groups, verticalGraph, horizontalGraph);

        var net1 = groups.Single(group => group.ContainsNet(1));
        var net2 = groups.Single(group => group.ContainsNet(2));
        var net4 = groups.Single(group => group.ContainsNet(4));

        Assert.True(horizontalGraph.Conflicts(1, 2));
        Assert.False(nonConstraintGraph.AreCompatible(net1, net2));
        Assert.True(nonConstraintGraph.AreCompatible(net2, net4));
    }

    [Fact]
    public void ZoneTable_UsesEventSweepLineRangesAndBoundaryCandidates()
    {
        var channel = new Channel(
            6,
            topRow:    new[] { 1, 0, 2, 0, 3, 0 },
            bottomRow: new[] { 0, 1, 0, 2, 0, 3 });
        var groups = channel.Nets.Values.Select(net => new CompositeNet(net)).ToList();

        var zones = ZoneTable.Build(channel, channel.Nets.Values.ToList());
        var boundaryPairs = zones.GetBoundaryCandidatePairs(groups).ToList();

        Assert.Contains(zones.Zones, zone =>
            zone.StartColumn == 0 && zone.EndColumn == 1 && zone.ActiveNetIds.SetEquals(new[] { 1 }));
        Assert.Contains(zones.Zones, zone =>
            zone.StartColumn == 2 && zone.EndColumn == 3 && zone.ActiveNetIds.SetEquals(new[] { 2 }));
        Assert.Contains(boundaryPairs, pair => pair.Left.ContainsNet(1) && pair.Right.ContainsNet(2));
        Assert.Contains(boundaryPairs, pair => pair.Left.ContainsNet(2) && pair.Right.ContainsNet(3));
    }

    [Fact]
    public void MergePlanner_SelectsZoneLocalIndependentBatchAndUpdatesStructures()
    {
        var channel = new Channel(
            8,
            topRow:    new[] { 1, 0, 2, 0, 3, 0, 4, 0 },
            bottomRow: new[] { 0, 1, 0, 2, 0, 3, 0, 4 });
        var nets = channel.Nets.Values.ToList();
        var graph = VerticalConstraintGraph.Build(channel);
        var planner = MergePlanner.Create(channel, nets, graph);
        var groups = nets.Select(net => new CompositeNet(net)).ToList();

        var firstBatch = planner.SelectZoneLocalMergeBatch(groups);
        var mergedGroups = planner.MergeCompatibleNets(nets);

        Assert.NotEmpty(firstBatch);
        Assert.All(firstBatch, candidate => Assert.True(candidate.Left.RightmostColumn < candidate.Right.LeftmostColumn));
        Assert.Single(mergedGroups);
        Assert.Equal(new[] { 1, 2, 3, 4 }, mergedGroups[0].NetIds);
    }

    [Fact]
    public void Graphs_UpdateAfterMergeReflectCompositeComponents()
    {
        var channel = new Channel(
            4,
            topRow:    new[] { 1, 0, 3, 0 },
            bottomRow: new[] { 2, 1, 2, 3 });
        var groups = channel.Nets.Values.Select(net => new CompositeNet(net)).ToList();
        var net1 = groups.Single(group => group.ContainsNet(1));
        var net3 = groups.Single(group => group.ContainsNet(3));
        var merged = net1.Merge(net3);
        var updatedGroups = groups.Where(group => !group.ContainsNet(1) && !group.ContainsNet(3)).Append(merged).ToList();

        var verticalGraph = VerticalConstraintGraph.Build(channel).UpdateAfterMerge(updatedGroups);
        var horizontalGraph = HorizontalConstraintGraph.Build(channel.Nets.Values).UpdateAfterMerge(updatedGroups);
        var zones = ZoneTable.Build(channel, channel.Nets.Values.ToList()).UpdateAfterMerge(updatedGroups, channel.Width);

        Assert.Contains(verticalGraph.Edges, edge => edge.From == merged.PrimaryNetId && edge.To == 2);
        Assert.True(horizontalGraph.Conflicts(merged, updatedGroups.Single(group => group.ContainsNet(2))));
        Assert.Contains(zones.Zones, zone => zone.ActiveNetIds.Contains(merged.PrimaryNetId));
    }

    [Fact]
    public void WeightedBipartiteMatcher_FindsExactMatchingInsteadOfGreedySingleBestEdge()
    {
        static CompositeNet Group(int id, int start, int end) => new(new Net(
            id,
            new List<Contact>
            {
                new(start, ContactPosition.Top, id),
                new(end, ContactPosition.Bottom, id)
            }));

        var leftA = Group(1, 0, 0);
        var leftB = Group(2, 2, 2);
        var rightX = Group(3, 4, 4);
        var rightY = Group(4, 6, 6);
        var candidates = new List<MergeCandidate>
        {
            new(leftA, rightX, LongestPathAfterMerge: 1, SharedZoneBoundaryScore: 1, Gap: 0),
            new(leftA, rightY, LongestPathAfterMerge: 1, SharedZoneBoundaryScore: 1, Gap: 0),
            new(leftB, rightX, LongestPathAfterMerge: 1, SharedZoneBoundaryScore: 1, Gap: 0)
        };

        var selected = WeightedBipartiteMatcher.FindMaximumWeightMatching(candidates);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, candidate => ReferenceEquals(candidate.Left, leftA) && ReferenceEquals(candidate.Right, rightY));
        Assert.Contains(selected, candidate => ReferenceEquals(candidate.Left, leftB) && ReferenceEquals(candidate.Right, rightX));
    }

    [Fact]
    public void VerticalConstraintGraph_BreakCyclesWithDoglegsProducesAcyclicRepair()
    {
        var channel = new Channel(
            2,
            topRow:    new[] { 1, 2 },
            bottomRow: new[] { 2, 1 });
        var graph = VerticalConstraintGraph.Build(channel);

        var plan = graph.BreakCyclesWithDoglegs(channel);
        var repairedOrder = plan.RepairedGraph.GetTopologicalOrder(channel);

        Assert.NotEmpty(plan.RelaxedEdges);
        Assert.Equal(2, repairedOrder.Count);
    }

}
