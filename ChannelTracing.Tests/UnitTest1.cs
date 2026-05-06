using src.Application.Algorithms;
using src.Domain.Entities;

namespace ChannelTracing.Tests;

/// <summary>
/// Literature-backed regression tests for classical channel-routing concepts:
/// left-edge interval coloring/channel density, VCG ordering, HCG non-overlap,
/// cyclic VCG constraints, and the two-layer horizontal/vertical segment model.
/// </summary>
public class RoutingAlgorithmsTests
{
    [Fact]
    public void LeftEdge_AssignsNonOverlappingIntervalsOnSameTrack_AndUsesDensityForNoVCGCase()
    {
        // Literature basis: in channel routing without vertical constraints, the left-edge
        // algorithm is interval-graph coloring; the number of tracks equals channel density.
        var channel = new Channel(
            6,
            topRow:    new[] { 1, 2, 0, 0, 0, 0 },
            bottomRow: new[] { 0, 0, 1, 2, 3, 3 });

        var result = new LeftEdgeAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(ComputeInclusiveDensity(channel), result.TracksUsed);
        Assert.Equal(2, result.TracksUsed);

        Assert.Equal(0, channel.Nets[1].AssignedTrack);
        Assert.Equal(1, channel.Nets[2].AssignedTrack);
        Assert.Equal(0, channel.Nets[3].AssignedTrack);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void LeftEdge_UsesCliqueDensityTracks_WhenAllIntervalsOverlap()
    {
        // Literature basis: channel density is the maximum number of net intervals
        // crossing a vertical cut; four mutually overlapping intervals require four tracks.
        var channel = new Channel(
            7,
            topRow:    new[] { 1, 2, 3, 4, 0, 0, 0 },
            bottomRow: new[] { 0, 0, 0, 1, 2, 3, 4 });

        var result = new LeftEdgeAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(ComputeInclusiveDensity(channel), result.TracksUsed);
        Assert.Equal(4, result.TracksUsed);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void LeftEdge_ReusesEarlierTrack_AfterIntervalGap()
    {
        // Literature basis: the left-edge algorithm greedily places the next interval on
        // the first track whose right edge is strictly left of the interval's left edge.
        var channel = new Channel(
            8,
            topRow:    new[] { 1, 2, 0, 0, 3, 0, 0, 0 },
            bottomRow: new[] { 0, 0, 1, 2, 0, 3, 4, 4 });

        var result = new LeftEdgeAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(ComputeInclusiveDensity(channel), result.TracksUsed);
        Assert.Equal(2, result.TracksUsed);
        Assert.Equal(channel.Nets[1].AssignedTrack, channel.Nets[3].AssignedTrack);
        Assert.Equal(channel.Nets[1].AssignedTrack, channel.Nets[4].AssignedTrack);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void LeftEdge_TreatsSharedEndpointAsHorizontalConflict()
    {
        // Literature basis: horizontal constraints are created by overlapping intervals.
        // In this discrete channel model endpoints are occupied columns, so [0,1] and [1,2]
        // both occupy column 1 and cannot share a track.
        var channel = new Channel(
            3,
            topRow:    new[] { 1, 2, 0 },
            bottomRow: new[] { 0, 1, 2 });

        var result = new LeftEdgeAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(ComputeInclusiveDensity(channel), result.TracksUsed);
        Assert.Equal(2, result.TracksUsed);
        Assert.NotEqual(channel.Nets[1].AssignedTrack, channel.Nets[2].AssignedTrack);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void Yoshimura_RespectsVerticalConstraintOrdering()
    {
        // Literature basis: a VCG edge top->bottom requires the top net to be routed
        // on a track above the bottom net.
        var channel = new Channel(
            4,
            topRow:    new[] { 1, 0, 3, 0 },
            bottomRow: new[] { 2, 1, 2, 3 });

        var result = new YoshimuraAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);

        var t1 = channel.Nets[1].AssignedTrack!.Value;
        var t2 = channel.Nets[2].AssignedTrack!.Value;
        var t3 = channel.Nets[3].AssignedTrack!.Value;

        Assert.True(t1 < t2, "Net 1 must be above net 2 due to VCG edge 1->2");
        Assert.True(t3 < t2, "Net 3 must be above net 2 due to VCG edge 3->2");
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void Yoshimura_UsesAtLeastLongestVerticalConstraintPathTracks()
    {
        // Literature basis: for dogleg-free two-layer channel routing, the longest path
        // in the vertical constraint graph is a lower bound on channel height.
        // This channel has the chain 1->2->3->4, so four ordered tracks are required.
        var channel = new Channel(
            4,
            topRow:    new[] { 1, 2, 3, 0 },
            bottomRow: new[] { 2, 3, 4, 1 });

        var result = new YoshimuraAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(4, result.TracksUsed);
        Assert.True(channel.Nets[1].AssignedTrack!.Value < channel.Nets[2].AssignedTrack!.Value);
        Assert.True(channel.Nets[2].AssignedTrack!.Value < channel.Nets[3].AssignedTrack!.Value);
        Assert.True(channel.Nets[3].AssignedTrack!.Value < channel.Nets[4].AssignedTrack!.Value);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void Yoshimura_CombinesDensityAndVcgLowerBounds()
    {
        // Literature basis: max(channel density, longest VCG path) is the classical
        // lower bound for dogleg-free two-layer channel routing.
        // Density is 3 at column 1 and the chain 1->2->3 also requires three tracks.
        var channel = new Channel(
            3,
            topRow:    new[] { 1, 2, 0 },
            bottomRow: new[] { 2, 3, 1 });

        var result = new YoshimuraAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(3, ComputeInclusiveDensity(channel));
        Assert.Equal(3, result.TracksUsed);
        Assert.True(channel.Nets[1].AssignedTrack!.Value < channel.Nets[2].AssignedTrack!.Value);
        Assert.True(channel.Nets[2].AssignedTrack!.Value < channel.Nets[3].AssignedTrack!.Value);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void Yoshimura_PreservesHorizontalNonOverlap_WhenVcgForcesHigherMinimumTrack()
    {
        // Literature basis: after VCG ordering sets a minimum track, the router must still
        // satisfy the horizontal constraint graph by moving to the next conflict-free track.
        // Edges: 1->2 and 1->3. Intervals of nets 2 and 3 overlap, so they cannot both use track 1.
        var channel = new Channel(
            5,
            topRow:    new[] { 1, 1, 0, 0, 0 },
            bottomRow: new[] { 2, 3, 2, 3, 0 });

        var result = new YoshimuraAlgorithm().Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Equal(3, result.TracksUsed);
        Assert.True(channel.Nets[1].AssignedTrack!.Value < channel.Nets[2].AssignedTrack!.Value);
        Assert.True(channel.Nets[1].AssignedTrack!.Value < channel.Nets[3].AssignedTrack!.Value);
        Assert.NotEqual(channel.Nets[2].AssignedTrack, channel.Nets[3].AssignedTrack);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Fact]
    public void Yoshimura_ReportsCycleInVerticalConstraintGraph()
    {
        // Literature basis: cyclic VCG constraints are impossible to satisfy in the
        // dogleg-free model without splitting nets.
        var channel = new Channel(
            2,
            topRow:    new[] { 1, 2 },
            bottomRow: new[] { 2, 1 });

        var result = new YoshimuraAlgorithm().Route(channel);

        Assert.True(result.HasConflicts);
        Assert.Contains(result.ConflictDescriptions, c => c.Contains("VCG contains cyclic constraints"));
    }

    [Fact]
    public void Yoshimura_CycleFallbackStillProducesHorizontalSegmentsWithoutOverlapConflicts()
    {
        // Literature basis: doglegs are normally needed to break VCG cycles. The current
        // simplified implementation reports the cycle and then routes greedily; it should
        // still avoid adding extra horizontal overlap conflicts.
        var channel = new Channel(
            4,
            topRow:    new[] { 1, 2, 0, 0 },
            bottomRow: new[] { 2, 1, 3, 3 });

        var result = new YoshimuraAlgorithm().Route(channel);

        Assert.True(result.HasConflicts);
        Assert.Single(result.ConflictDescriptions);
        Assert.Contains("VCG contains cyclic constraints", result.ConflictDescriptions[0]);
        AssertNoHorizontalOverlapOnSameTrack(result);
    }

    [Theory]
    [InlineData("left")]
    [InlineData("yoshimura")]
    public void TwoLayerModel_CreatesVerticalSegmentForSameColumnTopAndBottomTerminal(string algorithmName)
    {
        // Literature basis: two-layer channel routing uses horizontal wires, vertical wires,
        // and vias. In this project a same-column top/bottom terminal pair is represented
        // as a vertical segment on the assigned track.
        var channel = new Channel(
            3,
            topRow:    new[] { 1, 0, 2 },
            bottomRow: new[] { 1, 2, 0 });

        var result = CreateAlgorithm(algorithmName).Route(channel);

        Assert.False(result.HasConflicts);
        Assert.Contains(result.AllSegments, s =>
            s.NetId == 1 &&
            s.Type == SegmentType.Vertical &&
            s.StartColumn == 0 &&
            s.EndColumn == 0 &&
            s.Track == channel.Nets[1].AssignedTrack);
    }

    private static int ComputeInclusiveDensity(Channel channel)
    {
        var max = 0;

        for (var col = 0; col < channel.Width; col++)
        {
            var active = channel.Nets.Values.Count(net =>
                net.LeftmostColumn <= col && col <= net.RightmostColumn);
            max = Math.Max(max, active);
        }

        return max;
    }

    private static void AssertNoHorizontalOverlapOnSameTrack(RoutingResult result)
    {
        var horizontal = result.AllSegments
            .Where(s => s.Type == SegmentType.Horizontal)
            .OrderBy(s => s.Track)
            .ThenBy(s => s.StartColumn)
            .ToList();

        for (var i = 1; i < horizontal.Count; i++)
        {
            var previous = horizontal[i - 1];
            var current = horizontal[i];

            if (previous.Track != current.Track)
                continue;

            Assert.True(
                previous.EndColumn < current.StartColumn,
                $"Net {previous.NetId} [{previous.StartColumn}, {previous.EndColumn}] overlaps " +
                $"net {current.NetId} [{current.StartColumn}, {current.EndColumn}] on track {current.Track}");
        }
    }

    private static src.Application.Interfaces.IRoutingAlgorithm CreateAlgorithm(string algorithmName)
        => algorithmName switch
        {
            "left" => new LeftEdgeAlgorithm(),
            "yoshimura" => new YoshimuraAlgorithm(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithmName), algorithmName, null)
        };
}
