namespace src.Application.Algorithms.Yoshimura;

public sealed record ZoneBoundaryCandidateSet(
    int BoundaryColumn,
    int NextColumn,
    IReadOnlyList<CompositeNet> EndingGroups,
    IReadOnlyList<CompositeNet> StartingGroups);
