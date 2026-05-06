using src.Application.Interfaces;
using src.Domain.Entities;

namespace src.Application.Algorithms;

/// <summary>
/// Yoshimura-Kuh Channel Routing Algorithm.
///
/// Reference: T. Yoshimura and E. S. Kuh, "Efficient Algorithms for Channel Routing,"
/// IEEE Trans. on CAD of Integrated Circuits and Systems, vol. 1, pp. 25-35, 1982.
///
/// Pipeline:
///   1. Build VCG (Vertical Constraint Graph).
///   2. Build zone representation of the channel.
///   3. Net-merging phase: for each pair of adjacent zones merge nets
///      that (a) do not overlap horizontally and (b) are not connected
///      by a directed VCG path, choosing the best pair via the
///      cost functions f() / g() from the original paper.
///   4. Apply the Constrained Left-Edge Algorithm (CLE) on the merged
///      netlist to assign tracks.
///   5. Emit horizontal and vertical segments.
/// </summary>
public class YoshimuraAlgorithm : RoutingAlgorithmBase
{
    // Weight used in the paper's cost functions f() and g().
    private const int K = 100;

    public override string Name => "Yoshimura Algorithm";

    // -------------------------------------------------------------------------
    // Merged-net representation
    // A "virtual net" groups one or more original net IDs that have been merged.
    // Its horizontal span is the union of all constituent nets' spans.
    // -------------------------------------------------------------------------
    private sealed class VirtualNet
    {
        public int  Id            { get; }          // canonical ID (smallest member)
        public HashSet<int> Members { get; }        // original net IDs in this group
        public int  Left          { get; private set; }
        public int  Right         { get; private set; }

        public VirtualNet(int id, int left, int right)
        {
            Id      = id;
            Members = new HashSet<int> { id };
            Left    = left;
            Right   = right;
        }

        /// <summary>Extend the span to cover <paramref name="other"/>.</summary>
        public void AbsorbSpan(VirtualNet other)
        {
            if (other.Left  < Left)  Left  = other.Left;
            if (other.Right > Right) Right = other.Right;
            foreach (var m in other.Members)
                Members.Add(m);
        }

        public bool Overlaps(VirtualNet other) =>
            Left <= other.Right && other.Left <= Right;
    }

    // -------------------------------------------------------------------------
    protected override int ExecuteRouting(
        Channel        channel,
        List<Segment>  segments,
        List<string>   conflicts)
    {
        var nets   = channel.Nets.Values.ToList();
        var netIds = nets.Select(n => n.Id).ToList();

        // ------------------------------------------------------------------
        // 1. Build VCG
        // successors[a].Contains(b) means "a must be routed above b"
        // (a appears on the top row, b on the bottom row in the same column)
        // ------------------------------------------------------------------
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

        // ------------------------------------------------------------------
        // 2. Build zone representation
        // A new zone boundary is placed between column i and i+1 whenever
        // at least one net ends at column i AND at least one new net begins
        // at column i+1.
        // ------------------------------------------------------------------
        // For each column determine the set of active nets.
        var active = new List<HashSet<int>>(channel.Width);
        for (int col = 0; col < channel.Width; col++)
        {
            var set = new HashSet<int>();
            foreach (var net in nets)
                if (net.LeftmostColumn <= col && col <= net.RightmostColumn)
                    set.Add(net.Id);
            active.Add(set);
        }

        // Identify zone boundaries: columns that are the LAST column of a zone.
        var zoneBoundaries = new List<int>(); // last column index of each zone
        for (int col = 0; col < channel.Width - 1; col++)
        {
            bool someNetEndsHere  = active[col].Any(id => !active[col + 1].Contains(id));
            bool someNetStartsNext = active[col + 1].Any(id => !active[col].Contains(id));

            if (someNetEndsHere && someNetStartsNext)
                zoneBoundaries.Add(col);
        }
        zoneBoundaries.Add(channel.Width - 1); // last zone always ends at the last column

        // ------------------------------------------------------------------
        // 3. Net-merging phase
        //
        // Work with VirtualNet objects that can absorb multiple original nets.
        // mergeMap[originalId] → VirtualNet that currently represents it.
        // ------------------------------------------------------------------
        var virtualNets = new Dictionary<int, VirtualNet>();
        foreach (var net in nets)
            virtualNets[net.Id] = new VirtualNet(net.Id,
                net.LeftmostColumn, net.RightmostColumn);

        // VCG over virtual nets (re-mapped from original VCG).
        // We keep it separate to avoid mutating the channel's data.
        var vSucc = netIds.ToDictionary(id => id, id => new HashSet<int>(successors[id]));
        var vPred = netIds.ToDictionary(id => id, id => new HashSet<int>(predecessors[id]));

        // mergeMap: original net id → canonical virtual-net id
        var mergeMap = netIds.ToDictionary(id => id, id => id);

        int prevZoneEnd = -1;
        foreach (int zoneEnd in zoneBoundaries)
        {
            if (prevZoneEnd < 0) { prevZoneEnd = zoneEnd; continue; }

            // L = nets that end at prevZoneEnd and do NOT continue into [prevZoneEnd+1 .. zoneEnd]
            // R = nets that start in (prevZoneEnd .. zoneEnd] but were not in the previous zone
            var L = virtualNets.Values
                .Where(vn => vn.Right == prevZoneEnd)
                .ToHashSet();
            var R = virtualNets.Values
                .Where(vn => vn.Left > prevZoneEnd && vn.Left <= zoneEnd)
                .ToHashSet();

            MergeZones(L, R, virtualNets, vSucc, vPred, mergeMap, conflicts);

            prevZoneEnd = zoneEnd;
        }

        // ------------------------------------------------------------------
        // 4. Constrained Left-Edge (CLE) on the merged netlist
        // ------------------------------------------------------------------
        // Collect distinct virtual nets after merging.
        var distinctVnIds = mergeMap.Values.Distinct().ToList();

        // Rebuild in-degree over virtual nets.
        var vnInDeg = distinctVnIds.ToDictionary(id => id,
            id => vPred.TryGetValue(id, out var p) ? p.Count : 0);

        var ready = new SortedSet<(int left, int id)>(
            Comparer<(int left, int id)>.Create((a, b) =>
                a.left != b.left ? a.left.CompareTo(b.left) : a.id.CompareTo(b.id)));

        foreach (var id in distinctVnIds.Where(id => vnInDeg[id] == 0))
            ready.Add((virtualNets[id].Left, id));

        var topoOrder = new List<int>(distinctVnIds.Count);

        while (ready.Count > 0)
        {
            var first = ready.Min;
            ready.Remove(first);
            topoOrder.Add(first.id);

            if (!vSucc.TryGetValue(first.id, out var succs)) continue;
            foreach (var succ in succs)
            {
                if (--vnInDeg[succ] == 0)
                    ready.Add((virtualNets[succ].Left, succ));
            }
        }

        var cycleVnets = distinctVnIds.Except(topoOrder).ToList();
        if (cycleVnets.Count > 0)
        {
            conflicts.Add(
                "VCG contains cyclic constraints involving virtual nets: " +
                string.Join(", ", cycleVnets) +
                ". These cannot satisfy all vertical ordering requirements " +
                "simultaneously and are routed greedily.");

            topoOrder.AddRange(
                cycleVnets.OrderBy(id => virtualNets[id].Left));
        }

        // Assign tracks via left-edge with VCG constraint.
        var vnTrack      = new Dictionary<int, int>(distinctVnIds.Count);
        var trackIntervals = new Dictionary<int, List<(int start, int end)>>();

        foreach (var vnId in topoOrder)
        {
            var vn = virtualNets[vnId];

            int minTrack = vPred.TryGetValue(vnId, out var preds)
                ? preds.Where(p => vnTrack.ContainsKey(p))
                       .Select(p => vnTrack[p] + 1)
                       .DefaultIfEmpty(0)
                       .Max()
                : 0;

            int assignedTrack = minTrack;
            while (true)
            {
                if (!trackIntervals.TryGetValue(assignedTrack, out var ivals) ||
                    !HasHorizontalConflict(ivals, vn.Left, vn.Right))
                    break;
                assignedTrack++;
            }

            vnTrack[vnId] = assignedTrack;
            trackIntervals.TryAdd(assignedTrack, new List<(int, int)>());
            trackIntervals[assignedTrack].Add((vn.Left, vn.Right));
        }

        // ------------------------------------------------------------------
        // 5. Emit segments
        // Each original net inherits the track of its virtual net.
        // ------------------------------------------------------------------
        int maxTrack = 0;

        foreach (var net in nets)
        {
            int vnId  = mergeMap[net.Id];
            int track = vnTrack[vnId];
            net.AssignedTrack = track;

            if (track + 1 > maxTrack) maxTrack = track + 1;

            // Horizontal segment covering the original net's own span.
            var hSeg = new Segment(
                net.Id, SegmentType.Horizontal,
                net.LeftmostColumn, net.RightmostColumn, track);
            segments.Add(hSeg);
            net.AddSegment(hSeg);

            // Vertical segments for EVERY contact column (top-only, bottom-only,
            // or both). Each terminal must be connected to the horizontal wire.
            CreateVerticalSegments(net, track, segments);
        }

        DetectConflicts(segments, conflicts);
        return maxTrack;
    }

    // -------------------------------------------------------------------------
    // Net-merging helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Merge nets across a zone boundary.
    ///
    /// L = virtual nets ending at the left boundary.
    /// R = virtual nets beginning after the boundary.
    ///
    /// A pair (m ∈ L, n ∈ R) is mergeable if:
    ///   • they do not overlap horizontally (guaranteed by construction but
    ///     checked defensively), AND
    ///   • neither is reachable from the other in the current VCG.
    ///
    /// The best pair is chosen via the cost functions from the paper:
    ///   u(v) = |predecessors(v)|, d(v) = |successors(v)|
    ///   f(v) = K*(u+d) + max(u,d)        — used to pick m* from Q
    ///   g(n,m) = K*(u(n∪m)+d(n∪m)) + max(u(n∪m),d(n∪m))  — used to pick n* from P
    /// where P ⊆ L and Q ⊆ R partition the mergeable set.
    /// </summary>
    private static void MergeZones(
        HashSet<VirtualNet>          L,
        HashSet<VirtualNet>          R,
        Dictionary<int, VirtualNet>  virtualNets,
        Dictionary<int, HashSet<int>> vSucc,
        Dictionary<int, HashSet<int>> vPred,
        Dictionary<int, int>         mergeMap,
        List<string>                 conflicts)
    {
        while (true)
        {
            // Find all mergeable (m ∈ L, n ∈ R) pairs.
            var mergeable = (
                from m in L
                from n in R
                where !m.Overlaps(n)
                   && !AreOnSameVcgPath(m.Id, n.Id, vSucc)
                select (m, n)
            ).ToList();

            if (mergeable.Count == 0)
                break;

            // Build P ⊆ L and Q ⊆ R that participate in at least one mergeable pair.
            var P = mergeable.Select(p => p.m).ToHashSet();
            var Q = mergeable.Select(p => p.n).ToHashSet();

            // Choose m* from Q (paper notation — one side; here we let Q be R-side).
            var mStar = Q.MaxBy(n => CostF(n.Id, vSucc, vPred))!;

            // From P, choose n* that maximises g(n*, m*).
            var nStar = P
                .Where(m => mergeable.Any(p => p.m.Id == m.Id && p.n.Id == mStar.Id))
                .MaxBy(m => CostG(m.Id, mStar.Id, vSucc, vPred))!;

            // Perform the merge: nStar absorbs mStar.
            DoMerge(nStar, mStar, virtualNets, vSucc, vPred, mergeMap);

            // Remove merged partners from L and R.
            L.Remove(nStar);
            R.Remove(mStar);

            // If Q (R-side) is now empty we are done with this boundary.
            if (!R.Any()) break;
        }
    }

    /// <summary>
    /// Merge virtual net <paramref name="absorb"/> into <paramref name="target"/>.
    /// Afterwards every reference to absorb's id is redirected to target.
    /// The VCG is updated: target inherits all predecessors/successors of absorb.
    /// </summary>
    private static void DoMerge(
        VirtualNet                   target,
        VirtualNet                   absorb,
        Dictionary<int, VirtualNet>  virtualNets,
        Dictionary<int, HashSet<int>> vSucc,
        Dictionary<int, HashSet<int>> vPred,
        Dictionary<int, int>         mergeMap)
    {
        int tId = target.Id;
        int aId = absorb.Id;

        // Extend span.
        target.AbsorbSpan(absorb);

        // Remap all members of absorb → target.
        foreach (var m in absorb.Members)
        {
            mergeMap[m]     = tId;
            virtualNets[m]  = target;
        }

        // Update VCG: target inherits absorb's edges.
        if (vSucc.TryGetValue(aId, out var aSucc))
        {
            vSucc.TryAdd(tId, new HashSet<int>());
            foreach (var s in aSucc)
            {
                if (s != tId) vSucc[tId].Add(s);
                if (vPred.TryGetValue(s, out var sp))
                {
                    sp.Remove(aId);
                    if (s != tId) sp.Add(tId);
                }
            }
            vSucc.Remove(aId);
        }

        if (vPred.TryGetValue(aId, out var aPred))
        {
            vPred.TryAdd(tId, new HashSet<int>());
            foreach (var p in aPred)
            {
                if (p != tId) vPred[tId].Add(p);
                if (vSucc.TryGetValue(p, out var ps))
                {
                    ps.Remove(aId);
                    if (p != tId) ps.Add(tId);
                }
            }
            vPred.Remove(aId);
        }

        // Remove self-loops that may have been introduced.
        vSucc.GetValueOrDefault(tId)?.Remove(tId);
        vPred.GetValueOrDefault(tId)?.Remove(tId);

        virtualNets.Remove(aId);
    }

    // -------------------------------------------------------------------------
    // VCG reachability: BFS/DFS — "is there a directed path from a to b OR b to a?"
    // -------------------------------------------------------------------------
    private static bool AreOnSameVcgPath(
        int id1, int id2,
        Dictionary<int, HashSet<int>> vSucc)
        => IsReachable(id1, id2, vSucc) || IsReachable(id2, id1, vSucc);

    private static bool IsReachable(
        int from, int to,
        Dictionary<int, HashSet<int>> succ)
    {
        if (from == to) return true;
        var visited = new HashSet<int>();
        var stack   = new Stack<int>();
        stack.Push(from);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!visited.Add(cur)) continue;

            if (!succ.TryGetValue(cur, out var nexts)) continue;
            foreach (var n in nexts)
            {
                if (n == to) return true;
                if (!visited.Contains(n)) stack.Push(n);
            }
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Cost functions (Yoshimura-Kuh, 1982)
    //   u(v) = number of predecessors (nets that must be above v)
    //   d(v) = number of successors   (nets that must be below v)
    //   f(v) = K*(u+d) + max(u,d)
    //   g(n,m) = f evaluated on the merged net n∪m
    // -------------------------------------------------------------------------
    private static int U(int id, Dictionary<int, HashSet<int>> vPred) =>
        vPred.TryGetValue(id, out var p) ? p.Count : 0;

    private static int D(int id, Dictionary<int, HashSet<int>> vSucc) =>
        vSucc.TryGetValue(id, out var s) ? s.Count : 0;

    private static int CostF(
        int id,
        Dictionary<int, HashSet<int>> vSucc,
        Dictionary<int, HashSet<int>> vPred)
    {
        int u = U(id, vPred), d = D(id, vSucc);
        return K * (u + d) + Math.Max(u, d);
    }

    private static int CostG(
        int idM,
        int idN,
        Dictionary<int, HashSet<int>> vSucc,
        Dictionary<int, HashSet<int>> vPred)
    {
        // Simulate union of predecessors and successors of the merged net.
        var mergedPred = new HashSet<int>(
            vPred.TryGetValue(idM, out var pm) ? pm : Enumerable.Empty<int>());
        if (vPred.TryGetValue(idN, out var pn))
            foreach (var x in pn) mergedPred.Add(x);
        mergedPred.Remove(idM);
        mergedPred.Remove(idN);

        var mergedSucc = new HashSet<int>(
            vSucc.TryGetValue(idM, out var sm) ? sm : Enumerable.Empty<int>());
        if (vSucc.TryGetValue(idN, out var sn))
            foreach (var x in sn) mergedSucc.Add(x);
        mergedSucc.Remove(idM);
        mergedSucc.Remove(idN);

        int u = mergedPred.Count, d = mergedSucc.Count;
        return K * (u + d) + Math.Max(u, d);
    }

    // -------------------------------------------------------------------------
    // Segment helpers
    // -------------------------------------------------------------------------

    private static bool HasHorizontalConflict(
        List<(int start, int end)> intervals,
        int left,
        int right)
        => intervals.Any(iv => iv.start <= right && iv.end >= left);

    /// <summary>
    /// Creates a vertical segment for EVERY contact column of <paramref name="net"/>.
    ///
    /// BUG FIX: The previous implementation only created a vertical segment
    /// when both a top AND a bottom contact existed at the same column.
    /// This left top-only and bottom-only terminals unconnected to their
    /// horizontal track, which is incorrect.  Every terminal — regardless of
    /// whether the opposite row has a contact at that column — needs a vertical
    /// wire connecting it to the horizontal wire on the assigned track.
    /// </summary>
    private static void CreateVerticalSegments(
        Net net, int track, List<Segment> segments)
    {
        foreach (var col in net.Contacts.Select(c => c.Column).Distinct())
        {
            var vSeg = new Segment(
                net.Id, SegmentType.Vertical,
                col, col, track);
            segments.Add(vSeg);
            net.AddSegment(vSeg);
        }
    }
}