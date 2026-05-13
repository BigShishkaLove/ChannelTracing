using src.Domain.Entities;

namespace src.Application.Algorithms.Yoshimura;

public sealed class ZoneTable
{
    private readonly List<Zone> _zones;

    private ZoneTable(List<Zone> zones)
        => _zones = zones;

    public IReadOnlyList<Zone> Zones => _zones;

    public static ZoneTable Build(Channel channel, IReadOnlyCollection<Net> nets)
        => BuildFromIntervals(
            channel.Width,
            nets.Select(net => (net.Id, Start: net.LeftmostColumn, End: net.RightmostColumn)));

    public ZoneTable UpdateAfterMerge(IReadOnlyCollection<CompositeNet> groups, int channelWidth)
        => BuildFromIntervals(
            channelWidth,
            groups.SelectMany(group => group.Intervals.Select(iv => (group.PrimaryNetId, Start: iv.start, End: iv.end))));

    public IEnumerable<ZoneBoundaryCandidateSet> GetBoundaryCandidateSets(
        IReadOnlyCollection<CompositeNet> groups)
    {
        for (var i = 0; i < _zones.Count - 1; i++)
        {
            var boundaryColumn = _zones[i].EndColumn;
            var nextColumn = _zones[i + 1].StartColumn;
            var ending = groups
                .Where(group => group.EndsAt(boundaryColumn))
                .OrderBy(group => group, CompositeNetComparer.Instance)
                .ToList();
            var starting = groups
                .Where(group => group.StartsAt(nextColumn))
                .OrderBy(group => group, CompositeNetComparer.Instance)
                .ToList();

            if (ending.Count > 0 && starting.Count > 0)
                yield return new ZoneBoundaryCandidateSet(boundaryColumn, nextColumn, ending, starting);
        }
    }

    public IEnumerable<(CompositeNet Left, CompositeNet Right)> GetBoundaryCandidatePairs(
        IReadOnlyCollection<CompositeNet> groups)
    {
        var emitted = new HashSet<(CompositeNet Left, CompositeNet Right)>();

        foreach (var boundary in GetBoundaryCandidateSets(groups))
        {
            foreach (var left in boundary.EndingGroups)
            {
                foreach (var right in boundary.StartingGroups)
                {
                    if (ReferenceEquals(left, right))
                        continue;

                    var ordered = left.LeftmostColumn <= right.LeftmostColumn
                        ? (Left: left, Right: right)
                        : (Left: right, Right: left);

                    if (emitted.Add(ordered))
                        yield return ordered;
                }
            }
        }
    }

    public int SharedBoundaryScore(CompositeNet left, CompositeNet right)
    {
        var score = 0;

        for (var i = 0; i < _zones.Count - 1; i++)
        {
            var current = _zones[i];
            var next = _zones[i + 1];
            var ending = left.NetIds.Any(id => current.ActiveNetIds.Contains(id) && !next.ActiveNetIds.Contains(id));
            var starting = right.NetIds.Any(id => !current.ActiveNetIds.Contains(id) && next.ActiveNetIds.Contains(id));

            if (ending && starting)
                score++;

            ending = right.NetIds.Any(id => current.ActiveNetIds.Contains(id) && !next.ActiveNetIds.Contains(id));
            starting = left.NetIds.Any(id => !current.ActiveNetIds.Contains(id) && next.ActiveNetIds.Contains(id));

            if (ending && starting)
                score++;
        }

        return score;
    }

    private static ZoneTable BuildFromIntervals(
        int channelWidth,
        IEnumerable<(int Id, int Start, int End)> intervals)
    {
        var events = new SortedDictionary<int, List<(int Id, bool Add)>>();

        foreach (var interval in intervals)
        {
            AddEvent(events, interval.Start, interval.Id, add: true);
            AddEvent(events, interval.End + 1, interval.Id, add: false);
        }

        AddEvent(events, 0, 0, add: true);
        AddEvent(events, channelWidth, 0, add: false);

        var zones = new List<Zone>();
        var active = new HashSet<int>();
        int? previousColumn = null;

        foreach (var entry in events)
        {
            var column = entry.Key;
            var changes = entry.Value;

            if (previousColumn.HasValue && previousColumn.Value < column)
            {
                zones.Add(new Zone(
                    previousColumn.Value,
                    column - 1,
                    active.Where(id => id != 0).Order().ToHashSet()));
            }

            foreach (var change in changes)
            {
                if (change.Id == 0)
                    continue;

                if (change.Add)
                    active.Add(change.Id);
                else
                    active.Remove(change.Id);
            }

            previousColumn = column;
        }

        return new ZoneTable(zones);
    }

    private static void AddEvent(
        SortedDictionary<int, List<(int Id, bool Add)>> events,
        int column,
        int id,
        bool add)
    {
        if (!events.TryGetValue(column, out var changes))
        {
            changes = new List<(int, bool)>();
            events[column] = changes;
        }

        changes.Add((id, add));
    }
}
