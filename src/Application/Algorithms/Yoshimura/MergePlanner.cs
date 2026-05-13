using src.Domain.Entities;

namespace src.Application.Algorithms.Yoshimura;

public sealed class MergePlanner
{
    private readonly int _channelWidth;
    private readonly VerticalConstraintGraph _originalVerticalGraph;
    private VerticalConstraintGraph _componentVerticalGraph;
    private HorizontalConstraintGraph _horizontalGraph;
    private HorizontalNonConstraintGraph _horizontalNonConstraintGraph;
    private ZoneTable _zoneTable;

    public MergePlanner(
        int channelWidth,
        VerticalConstraintGraph verticalGraph,
        HorizontalConstraintGraph horizontalGraph,
        ZoneTable zoneTable,
        IReadOnlyCollection<CompositeNet> initialGroups)
    {
        _channelWidth = channelWidth;
        _originalVerticalGraph = verticalGraph;
        _componentVerticalGraph = verticalGraph;
        _horizontalGraph = horizontalGraph;
        _zoneTable = zoneTable;
        _horizontalNonConstraintGraph = HorizontalNonConstraintGraph.Build(initialGroups, _originalVerticalGraph, horizontalGraph);
    }

    public static MergePlanner Create(Channel channel, IReadOnlyCollection<Net> nets, VerticalConstraintGraph verticalGraph)
    {
        var initialGroups = nets.Select(net => new CompositeNet(net)).ToList();
        var horizontalGraph = HorizontalConstraintGraph.Build(nets);
        var zoneTable = ZoneTable.Build(channel, nets);
        return new MergePlanner(channel.Width, verticalGraph, horizontalGraph, zoneTable, initialGroups);
    }

    public List<CompositeNet> MergeCompatibleNets(IEnumerable<Net> nets)
    {
        var groups = nets.Select(n => new CompositeNet(n)).ToList();
        var changed = true;

        while (changed)
        {
            changed = false;
            var selected = CreateZoneScanner().SelectMergeBatch(groups);
            if (selected.Count == 0)
                continue;

            var used = new HashSet<CompositeNet>();
            var nextGroups = new List<CompositeNet>();

            foreach (var candidate in selected)
            {
                if (!used.Add(candidate.Left) || !used.Add(candidate.Right))
                    continue;

                nextGroups.Add(candidate.Left.Merge(candidate.Right));
                changed = true;
            }

            foreach (var group in groups)
            {
                if (!used.Contains(group))
                    nextGroups.Add(group);
            }

            groups = nextGroups;

            // Keep all Yoshimura/Kuh structures synchronized with the new composite
            // net set after every merge pass.
            _componentVerticalGraph = _componentVerticalGraph.UpdateAfterMerge(groups);
            _horizontalGraph = _horizontalGraph.UpdateAfterMerge(groups);
            _horizontalNonConstraintGraph = _horizontalNonConstraintGraph.UpdateAfterMerge(
                groups,
                _originalVerticalGraph,
                _horizontalGraph);
            _zoneTable = _zoneTable.UpdateAfterMerge(groups, _channelWidth);
        }

        return groups
            .OrderBy(g => g.LeftmostColumn)
            .ThenBy(g => g.RightmostColumn)
            .ThenBy(g => g.PrimaryNetId)
            .ToList();
    }

    public List<MergeCandidate> SelectZoneLocalMergeBatch(List<CompositeNet> groups)
        => CreateZoneScanner().SelectMergeBatch(groups);

    private YoshimuraZoneScanner CreateZoneScanner()
        => new(_originalVerticalGraph, _horizontalNonConstraintGraph, _zoneTable);
}
