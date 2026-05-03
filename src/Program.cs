using src.Application.Algorithms;
using src.Application.Interfaces;
using src.Application.Services;
using src.Infrastructure.IO;

namespace src;

internal static class Program
{
    static int Main(string[] args)
    {
        var inputPath = args.Length > 0 ? args[0] : null;
        var outputDir = args.Length > 1 ? args[1] : "output";
        var mode = args.Length > 2 ? args[2].ToLowerInvariant() : "both";

        Directory.CreateDirectory(outputDir);

        var channel = inputPath is not null
            ? new ChannelFileReader().ReadFromFile(inputPath)
            : new ChannelDataGenerator(seed: 42).GenerateSimpleChannel(100, 40);

        var algorithms = ResolveAlgorithms(mode);
        var store = new RoutingResultStore(outputDir);
        var writer = new ChannelFileWriter();

        foreach (var algorithm in algorithms)
        {
            var result = algorithm.Route(channel);
            store.Save(result);
            writer.WriteResultToFile(result, Path.Combine(outputDir, $"{RoutingResultStore.Slug(result.AlgorithmName)}.txt"));
        }

        return 0;
    }

    private static IReadOnlyList<IRoutingAlgorithm> ResolveAlgorithms(string mode) => mode switch
    {
        "left" => [new LeftEdgeAlgorithm()],
        "yoshimura" => [new YoshimuraAlgorithm()],
        _ => [new LeftEdgeAlgorithm(), new YoshimuraAlgorithm()]
    };
}
