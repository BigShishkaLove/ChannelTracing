using src.Application.Algorithms;
using src.Application.Services;
using src.Infrastructure.IO;
using src.Infrastructure.Visualization;
using src.Domain.Entities;

namespace src;

class Program
{
    private static readonly ConsoleVisualizer    _consoleViz = new();
    private static readonly SvgVisualizer        _svgViz     = new();
    private static readonly ChannelDataGenerator _generator  = new();
    private static readonly ChannelFileReader    _fileReader  = new();
    private static readonly ChannelFileWriter    _fileWriter  = new();

    private static readonly LeftEdgeAlgorithm  _leftEdge  = new();
    private static readonly YoshimuraAlgorithm _yoshimura = new();

    // ----------------------------------------------------------------
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
                case "1": RunSimpleExample();        break;
                case "2": RunYoshimuraExample();     break;
                case "3": RunAlgorithmComparison();  break;
                case "4": RunCustomChannel();        break;
                case "5": RunFromFile();             break;
                case "6": RunBenchmark();            break;
                case "7": RunChannelWithConflicts(); break;
                case "8": GenerateTestData();        break;
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

    // ----------------------------------------------------------------
    private static void PrintWelcome()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Channel Routing — Left-Edge & Yoshimura Demo            ║");
        Console.WriteLine("║   VLSI CAD Tool — Circuit Net Routing                     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintMenu()
    {
        Console.WriteLine("\n=== Main Menu ===");
        Console.WriteLine("1. Run Left-Edge Example      (10 cols, 4 nets)");
        Console.WriteLine("2. Run Yoshimura Example      (10 cols, 4 nets)");
        Console.WriteLine("3. Compare Both Algorithms    (side-by-side)");
        Console.WriteLine("4. Create Custom Channel");
        Console.WriteLine("5. Load Channel from File");
        Console.WriteLine("6. Run Benchmark              (various sizes)");
        Console.WriteLine("7. Generate Channel with Conflicts");
        Console.WriteLine("8. Generate Test Data Files");
        Console.WriteLine("0. Exit");
        Console.Write("\nYour choice: ");
    }

    // ────────────────────────────────────────────────────────────────
    // 1. Left-Edge example
    // ────────────────────────────────────────────────────────────────
    private static void RunSimpleExample()
    {
        Console.WriteLine("\n=== Left-Edge Example ===\n");
        var channel = _generator.GenerateSimpleChannel(width: 10, netCount: 4);
        _consoleViz.DisplayChannel(channel);

        var result = _leftEdge.Route(channel);
        _consoleViz.DisplayRoutingResult(result);

        SaveSvg(result, "simple_left_edge.svg");
    }

    // ────────────────────────────────────────────────────────────────
    // 2. Yoshimura example
    // ────────────────────────────────────────────────────────────────
    private static void RunYoshimuraExample()
    {
        Console.WriteLine("\n=== Yoshimura Example ===\n");

        var gen     = new ChannelDataGenerator(seed: 42);
        var channel = gen.GenerateSimpleChannel(width: 10, netCount: 4);
        _consoleViz.DisplayChannel(channel);

        var result = _yoshimura.Route(channel);
        _consoleViz.DisplayRoutingResult(result);

        PrintVCGInfo(channel);
        SaveSvg(result, "yoshimura_example.svg");
    }

    // ────────────────────────────────────────────────────────────────
    // 3. Side-by-side comparison
    // ────────────────────────────────────────────────────────────────
    private static void RunAlgorithmComparison()
    {
        Console.WriteLine("\n=== Algorithm Comparison ===\n");

        Console.Write("Channel width  [default 12]: ");
        var wInput = Console.ReadLine()?.Trim();
        int width  = int.TryParse(wInput, out int w) && w > 0 ? w : 12;

        Console.Write("Number of nets [default  5]: ");
        var nInput   = Console.ReadLine()?.Trim();
        int netCount = int.TryParse(nInput, out int n) && n > 0 ? n : 5;

        var gen     = new ChannelDataGenerator(seed: 7);
        var channel = gen.GenerateSimpleChannel(width, netCount);

        _consoleViz.DisplayChannel(channel);
        PrintVCGInfo(channel);

        var leResult   = _leftEdge.Route(channel);
        var yoshResult = _yoshimura.Route(channel);

        Console.WriteLine("\n─── Left-Edge ───────────────────────────────────");
        _consoleViz.DisplayRoutingResult(leResult);

        Console.WriteLine("\n─── Yoshimura ───────────────────────────────────");
        _consoleViz.DisplayRoutingResult(yoshResult);

        _consoleViz.DisplayComparison(new List<RoutingResult> { leResult, yoshResult });

        Directory.CreateDirectory("output");
        _svgViz.SaveToFile(leResult,   Path.Combine("output", "compare_left_edge.svg"));
        _svgViz.SaveToFile(yoshResult, Path.Combine("output", "compare_yoshimura.svg"));
        Console.WriteLine("SVG files saved to output/compare_*.svg");
    }

    // ────────────────────────────────────────────────────────────────
    // 4. Custom channel
    // ────────────────────────────────────────────────────────────────
    private static void RunCustomChannel()
    {
        Console.WriteLine("\n=== Custom Channel ===\n");

        Console.Write("Enter channel width: ");
        if (!int.TryParse(Console.ReadLine(), out int width) || width <= 0)
        { Console.WriteLine("Invalid width!"); return; }

        Console.Write("Enter number of nets: ");
        if (!int.TryParse(Console.ReadLine(), out int netCount) || netCount <= 0)
        { Console.WriteLine("Invalid net count!"); return; }

        Console.Write("Algorithm? [1] Left-Edge  [2] Yoshimura  [3] Both : ");
        var algoChoice = Console.ReadLine()?.Trim();

        var channel = _generator.GenerateSimpleChannel(width, netCount);
        _consoleViz.DisplayChannel(channel);
        PrintVCGInfo(channel);

        Directory.CreateDirectory("output");

        switch (algoChoice)
        {
            case "2":
                RunAndSave(_yoshimura, channel, "custom_yoshimura");
                break;
            case "3":
                RunAndSave(_leftEdge,  channel, "custom_left_edge");
                RunAndSave(_yoshimura, channel, "custom_yoshimura");
                _consoleViz.DisplayComparison(new List<RoutingResult>
                {
                    _leftEdge.Route(channel),
                    _yoshimura.Route(channel)
                });
                break;
            default:
                RunAndSave(_leftEdge, channel, "custom_left_edge");
                break;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 5. Load from file
    // ────────────────────────────────────────────────────────────────
    private static void RunFromFile()
    {
        Console.WriteLine("\n=== Load from File ===\n");

        Console.Write("Enter file path: ");
        var filePath = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        { Console.WriteLine("File not found!"); return; }

        try
        {
            var channel = _fileReader.ReadFromFile(filePath);
            _consoleViz.DisplayChannel(channel);
            PrintVCGInfo(channel);

            Console.Write("Algorithm? [1] Left-Edge  [2] Yoshimura  [3] Both : ");
            var algoChoice = Console.ReadLine()?.Trim();

            Directory.CreateDirectory("output");

            if (algoChoice == "2" || algoChoice == "3")
            {
                var r = _yoshimura.Route(channel);
                _consoleViz.DisplayRoutingResult(r);
                _svgViz.SaveToFile(r, Path.Combine("output", "loaded_yoshimura.svg"));
            }
            if (algoChoice is "1" or null or "" || algoChoice == "3")
            {
                var r = _leftEdge.Route(channel);
                _consoleViz.DisplayRoutingResult(r);
                _svgViz.SaveToFile(r, Path.Combine("output", "loaded_left_edge.svg"));
            }
            if (algoChoice == "3")
            {
                _consoleViz.DisplayComparison(new List<RoutingResult>
                {
                    _leftEdge.Route(channel),
                    _yoshimura.Route(channel)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 6. Benchmark
    // ────────────────────────────────────────────────────────────────
    private static void RunBenchmark()
    {
        Console.WriteLine("\n=== Benchmark ===\n");

        var testCases = new[]
        {
            (Width:  10, NetCount:   5),
            (Width:  20, NetCount:  10),
            (Width:  50, NetCount:  25),
            (Width: 100, NetCount:  50),
            (Width: 200, NetCount: 100),
        };

        var leResults   = new List<RoutingResult>();
        var yoshResults = new List<RoutingResult>();

        foreach (var (width, netCount) in testCases)
        {
            Console.WriteLine($"Testing: {width} columns, {netCount} nets…");
            var channel = _generator.GenerateSimpleChannel(width, netCount);
            leResults.Add(_leftEdge.Route(channel));
            yoshResults.Add(_yoshimura.Route(channel));
        }

        PrintBenchmarkTable("Left-Edge", leResults,
            testCases.Select(t => (t.Width, t.NetCount)).ToArray());
        PrintBenchmarkTable("Yoshimura", yoshResults,
            testCases.Select(t => (t.Width, t.NetCount)).ToArray());

        Console.WriteLine("\n=== Delta (Yoshimura − Left-Edge) ===");
        Console.WriteLine($"{"Size",-10} | {"ΔTracks",8} | {"ΔWire",10} | {"ΔTime (ms)",12}");
        Console.WriteLine(new string('-', 50));

        for (int i = 0; i < leResults.Count; i++)
        {
            var lm   = leResults[i].GetMetrics();
            var ym   = yoshResults[i].GetMetrics();
            var size = $"{testCases[i].Width}×{testCases[i].NetCount}";
            Console.WriteLine(
                $"{size,-10} | {ym.TracksUsed - lm.TracksUsed,+8} | " +
                $"{ym.TotalWireLength - lm.TotalWireLength,+10:F0} | " +
                $"{ym.ExecutionTimeMs - lm.ExecutionTimeMs,+12:F2}");
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 7. Conflicts demo
    // ────────────────────────────────────────────────────────────────
    private static void RunChannelWithConflicts()
    {
        Console.WriteLine("\n=== Channel with Conflicts ===\n");
        Console.WriteLine("Generating channel with potential routing conflicts…\n");

        var channel = _generator.GenerateChannelWithConflicts(
            width: 15, netCount: 8, conflictCount: 2);

        _consoleViz.DisplayChannel(channel);
        PrintVCGInfo(channel);

        Directory.CreateDirectory("output");

        Console.WriteLine("\n— Left-Edge —");
        var leResult = _leftEdge.Route(channel);
        _consoleViz.DisplayRoutingResult(leResult);
        _svgViz.SaveToFile(leResult, Path.Combine("output", "conflicts_left_edge.svg"));

        Console.WriteLine("\n— Yoshimura —");
        var yoshResult = _yoshimura.Route(channel);
        _consoleViz.DisplayRoutingResult(yoshResult);
        _svgViz.SaveToFile(yoshResult, Path.Combine("output", "conflicts_yoshimura.svg"));

        _consoleViz.DisplayComparison(new List<RoutingResult> { leResult, yoshResult });
        Console.WriteLine("SVG files saved to output/conflicts_*.svg");
    }

    // ────────────────────────────────────────────────────────────────
    // 8. Generate test data
    // ────────────────────────────────────────────────────────────────
    private static void GenerateTestData()
    {
        Console.WriteLine("\n=== Generate Test Data ===\n");
        Directory.CreateDirectory("testdata");

        _fileWriter.WriteToFile(_generator.GenerateSimpleChannel(10, 4),
            "testdata/simple_10x4.txt");
        _fileWriter.WriteToFile(_generator.GenerateSimpleChannel(20, 10),
            "testdata/simple_20x10.txt");
        _fileWriter.WriteToFile(_generator.GenerateChannelWithConflicts(15, 8, 2),
            "testdata/conflicts_15x8.txt");
        _fileWriter.WriteToFile(_generator.GenerateSimpleChannel(100, 50),
            "testdata/large_100x50.txt");

        Console.WriteLine("Test data generated in 'testdata/' directory:");
        Console.WriteLine("  - simple_10x4.txt");
        Console.WriteLine("  - simple_20x10.txt");
        Console.WriteLine("  - conflicts_15x8.txt");
        Console.WriteLine("  - large_100x50.txt");
    }

    // ────────────────────────────────────────────────────────────────
    // Utility helpers
    // ────────────────────────────────────────────────────────────────

    private static void RunAndSave(
        src.Application.Interfaces.IRoutingAlgorithm algo,
        Channel channel,
        string baseName)
    {
        var result = algo.Route(channel);
        _consoleViz.DisplayRoutingResult(result);

        var svgPath = Path.Combine("output", baseName + ".svg");
        var txtPath = Path.Combine("output", baseName + "_result.txt");
        _svgViz.SaveToFile(result, svgPath);
        _fileWriter.WriteResultToFile(result, txtPath);
        Console.WriteLine($"Saved: {svgPath}  |  {txtPath}");
    }

    private static void SaveSvg(RoutingResult result, string fileName)
    {
        Directory.CreateDirectory("output");
        var path = Path.Combine("output", fileName);
        _svgViz.SaveToFile(result, path);
        Console.WriteLine($"SVG saved to: {path}");
    }

    /// <summary>
    /// Prints a human-readable dump of the VCG edges for a channel.
    /// </summary>
    private static void PrintVCGInfo(Channel channel)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("── Vertical Constraint Graph (VCG) ──");

        bool anyEdge = false;
        for (int col = 0; col < channel.Width; col++)
        {
            int top = channel.TopRow[col];
            int bot = channel.BottomRow[col];
            if (top != 0 && bot != 0 && top != bot)
            {
                Console.WriteLine($"  Net {top,2} must be ABOVE Net {bot,2}  (column {col})");
                anyEdge = true;
            }
        }
        if (!anyEdge)
            Console.WriteLine("  (no vertical constraints)");

        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintBenchmarkTable(
        string label,
        List<RoutingResult> results,
        (int Width, int NetCount)[] cases)
    {
        Console.WriteLine($"\n=== {label} ===");
        Console.WriteLine($"{"Size",-10} | {"Tracks",6} | {"Wire",10} | {"Time (ms)",10}");
        Console.WriteLine(new string('-', 46));
        for (int i = 0; i < results.Count; i++)
        {
            var m    = results[i].GetMetrics();
            var size = $"{cases[i].Width}×{cases[i].NetCount}";
            Console.WriteLine(
                $"{size,-10} | {m.TracksUsed,6} | " +
                $"{m.TotalWireLength,10:F0} | {m.ExecutionTimeMs,10:F2}");
        }
    }
}