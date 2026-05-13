namespace src.Application.Algorithms.Yoshimura;

public sealed record DoglegSplitPlan(
    VerticalConstraintGraph RepairedGraph,
    IReadOnlyList<(int From, int To)> RelaxedEdges);
