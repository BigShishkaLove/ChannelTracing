namespace src.Application.Algorithms.Yoshimura;

public sealed record Zone(int StartColumn, int EndColumn, HashSet<int> ActiveNetIds);
