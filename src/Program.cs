using src.Application.Algorithms;
using src.Application.Services;
using src.Infrastructure.IO;
using src.Infrastructure.Visualization;
using src.Domain.Entities;

/// <summary>
/// Main console application for channel routing demonstration
/// </summary>

namespace src;
class Program
{
    private static readonly ConsoleVisualizer _consoleViz = new();
    private static readonly SvgVisualizer _svgViz = new();
    private static readonly HtmlVisualizer _htmlViz = new();
    private static readonly ChannelDataGenerator _generator = new();
    private static readonly ChannelFileReader _fileReader = new();
    private static readonly ChannelFileWriter _fileWriter = new();

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        PrintWelcome();

        bool running = true;
        while (running)
        {
            PrintMenu();
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    RunSimpleExample();
                    break;
                case "2":
                    RunCustomChannel();
                    break;
                case "3":
                    RunFromFile();
                    break;
                case "4":
                    RunBenchmark();
                    break;
                case "5":
                    RunChannelWithConflicts();
                    break;
                case "6":
                    GenerateTestData();
                    break;
                case "7":
                    RunComparison();
                    break;
                case "0":
                    running = false;
                    Console.WriteLine("Goodbye!");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }

            if (running)
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    private static void PrintWelcome()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     Channel Routing - Left-Edge Algorithm Demo             ║");
        Console.WriteLine("║     VLSI CAD Tool - Circuit Net Routing                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintMenu()
    {
        Console.WriteLine("\n=== Main Menu ===");
        Console.WriteLine("1. Run Simple Example (10 columns, 4 nets)");
        Console.WriteLine("2. Create Custom Channel");
        Console.WriteLine("3. Load Channel from File");
        Console.WriteLine("4. Run Benchmark (various sizes)");
        Console.WriteLine("5. Generate Channel with Conflicts");
        Console.WriteLine("6. Generate Test Data Files");
        Console.WriteLine("7. Compare Algorithms (HTML Report)");
        Console.WriteLine("0. Exit");
        Console.Write("\nYour choice: ");
    }

    private static void RunSimpleExample()
    {
        Console.WriteLine("\n=== Simple Example ===\n");

        var channel = _generator.GenerateSimpleChannel(width: 10, netCount: 4);
        _consoleViz.DisplayChannel(channel);

        var algorithm = new LeftEdgeAlgorithm();
        var result = algorithm.Route(channel);

        _consoleViz.DisplayRoutingResult(result);

        // Save SVG
        var svgPath = Path.Combine("output", "simple_example.svg");
        Directory.CreateDirectory("output");
        _svgViz.SaveToFile(result, svgPath);
        var htmlPath = Path.Combine("output", "report.html");
        _htmlViz.SaveToFile(result, htmlPath);
        Console.WriteLine($"HTML report: {htmlPath}");
        Console.WriteLine($"SVG visualization saved to: {svgPath}");
    }

    private static void RunCustomChannel()
    {
        Console.WriteLine("\n=== Custom Channel ===\n");

        Console.Write("Enter channel width: ");
        if (!int.TryParse(Console.ReadLine(), out int width) || width <= 0)
        {
            Console.WriteLine("Invalid width!");
            return;
        }

        Console.Write("Enter number of nets: ");
        if (!int.TryParse(Console.ReadLine(), out int netCount) || netCount <= 0)
        {
            Console.WriteLine("Invalid net count!");
            return;
        }

        var channel = _generator.GenerateSimpleChannel(width, netCount);
        _consoleViz.DisplayChannel(channel);

        var algorithm = new LeftEdgeAlgorithm();
        var result = algorithm.Route(channel);

        _consoleViz.DisplayRoutingResult(result);

        var svgPath = Path.Combine("output", "custom_channel.svg");
        var txtPath = Path.Combine("output", "custom_channel_result.txt");
        Directory.CreateDirectory("output");

        _svgViz.SaveToFile(result, svgPath);
        _fileWriter.WriteResultToFile(result, txtPath);

        Console.WriteLine($"\nResults saved:");
        Console.WriteLine($"  SVG: {svgPath}");
        Console.WriteLine($"  Text: {txtPath}");
    }

    private static void RunFromFile()
    {
        Console.WriteLine("\n=== Load from File ===\n");

        Console.Write("Enter file path: ");
        var filePath = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("File not found!");
            return;
        }

        try
        {
            var channel = _fileReader.ReadFromFile(filePath);
            _consoleViz.DisplayChannel(channel);

            var algorithm = new LeftEdgeAlgorithm();
            var result = algorithm.Route(channel);

            _consoleViz.DisplayRoutingResult(result);

            var svgPath = Path.Combine("output", "loaded_channel.svg");
            Directory.CreateDirectory("output");
            _svgViz.SaveToFile(result, svgPath);
            var htmlPath = Path.Combine("output", "report.html");
            _htmlViz.SaveToFile(result, htmlPath);
            Console.WriteLine($"HTML report: {htmlPath}");
            Console.WriteLine($"SVG visualization saved to: {svgPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void RunBenchmark()
    {
        Console.WriteLine("\n=== Benchmark ===\n");

        var testCases = new[]
        {
            (Width: 10, NetCount: 5),
            (Width: 20, NetCount: 10),
            (Width: 50, NetCount: 25),
            (Width: 100, NetCount: 50),
            (Width: 200, NetCount: 100)
        };

        var algorithm = new LeftEdgeAlgorithm();
        var results = new List<Domain.Entities.RoutingResult>();

        foreach (var (width, netCount) in testCases)
        {
            Console.WriteLine($"Testing: {width} columns, {netCount} nets...");
            var channel = _generator.GenerateSimpleChannel(width, netCount);
            var result = algorithm.Route(channel);
            results.Add(result);
        }

        Console.WriteLine("\nBenchmark Results:");
        Console.WriteLine($"{"Size (W×N)",-15} | {"Tracks",6} | {"Wire Length",11} | {"Time (ms)",10}");
        Console.WriteLine(new string('-', 55));

        foreach (var result in results)
        {
            var metrics = result.GetMetrics();
            var size = $"{result.Channel.Width}×{result.Channel.Nets.Count}";
            Console.WriteLine($"{size,-15} | {metrics.TracksUsed,6} | {metrics.TotalWireLength,11:F0} | {metrics.ExecutionTimeMs,10:F2}");
        }
    }

    private static void RunChannelWithConflicts()
    {
        Console.WriteLine("\n=== Channel with Conflicts ===\n");

        Console.WriteLine("Generating channel with potential routing conflicts...\n");

        var channel = _generator.GenerateChannelWithConflicts(
            width: 15,
            netCount: 8,
            conflictCount: 2
        );

        _consoleViz.DisplayChannel(channel);

        var algorithm = new LeftEdgeAlgorithm();
        var result = algorithm.Route(channel);

        _consoleViz.DisplayRoutingResult(result);

        var svgPath = Path.Combine("output", "conflicts_channel.svg");
        Directory.CreateDirectory("output");
        _svgViz.SaveToFile(result, svgPath);
        Console.WriteLine($"\nSVG visualization saved to: {svgPath}");
    }

    private static void RunComparison()
    {
        Console.WriteLine("\n=== Algorithm Comparison ===\n");

        Console.Write("Enter channel width: ");
        if (!int.TryParse(Console.ReadLine(), out int width) || width <= 0)
        { Console.WriteLine("Invalid width!"); return; }

        Console.Write("Enter number of nets: ");
        if (!int.TryParse(Console.ReadLine(), out int netCount) || netCount <= 0)
        { Console.WriteLine("Invalid net count!"); return; }

        var channel = _generator.GenerateSimpleChannel(width, netCount);
        _consoleViz.DisplayChannel(channel);

        var result1 = new LeftEdgeAlgorithm().Route(channel);
        var result2 = new LeftEdgeWithVcgAlgorithm().Route(channel);

        _consoleViz.DisplayComparison(new List<RoutingResult> { result1, result2 });

        Directory.CreateDirectory("output");
        var htmlPath = Path.Combine("output", "comparison_report.html");
        _htmlViz.SaveToFile(new[] { result1, result2 }, htmlPath);

        Console.WriteLine($"HTML report saved to: {htmlPath}");

        var fullPath = Path.GetFullPath(htmlPath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName  = fullPath,
            UseShellExecute = true
        });
    }

    private static void GenerateTestData()
    {
        Console.WriteLine("\n=== Generate Test Data ===\n");

        Directory.CreateDirectory("testdata");

        // Simple test cases
        var simple1 = _generator.GenerateSimpleChannel(10, 4);
        _fileWriter.WriteToFile(simple1, "testdata/simple_10x4.txt");

        var simple2 = _generator.GenerateSimpleChannel(20, 10);
        _fileWriter.WriteToFile(simple2, "testdata/simple_20x10.txt");

        // With conflicts
        var conflicts = _generator.GenerateChannelWithConflicts(15, 8, 2);
        _fileWriter.WriteToFile(conflicts, "testdata/conflicts_15x8.txt");

        // Large test case
        var large = _generator.GenerateSimpleChannel(100, 50);
        _fileWriter.WriteToFile(large, "testdata/large_100x50.txt");

        Console.WriteLine("Test data generated in 'testdata/' directory:");
        Console.WriteLine("  - simple_10x4.txt");
        Console.WriteLine("  - simple_20x10.txt");
       // Console.WriteLine("  - conflicts_15x8.txt");
        Console.WriteLine("  - large_100x50.txt");
    }
}