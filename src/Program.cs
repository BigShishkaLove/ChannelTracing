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
    private static readonly HtmlVisualizer       _htmlViz    = new();
    private static readonly RoutingResultStore   _store      = new("output");
    private static readonly ChannelDataGenerator _generator  = new();
    private static readonly ChannelFileReader    _fileReader  = new();
    private static readonly ChannelFileWriter    _fileWriter  = new();

    private static readonly LeftEdgeAlgorithm  _leftEdge  = new();
    private static readonly YoshimuraAlgorithm _yoshimura = new();

    // ── Entry point ──────────────────────────────────────────────────
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
                case "3": RunCustomChannel();        break;
                case "4": RunFromFile();             break;
                case "5": RunBenchmark();            break;
                case "6": RunChannelWithConflicts(); break;
                case "7": RunHtmlReport();           break;
                case "8": GenerateTestData();        break;
                case "0": running = false; Console.WriteLine("Goodbye!"); break;
                default:  Console.WriteLine("Invalid choice. Please try again."); break;
            }

            if (running)
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    // ── Welcome / menu ───────────────────────────────────────────────
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
        var leOk   = _store.Exists(_leftEdge.Name)  ? " ✓" : "";
        var yoshOk = _store.Exists(_yoshimura.Name) ? " ✓" : "";

        Console.WriteLine("\n=== Main Menu ===");
        Console.WriteLine($"1. Left-Edge Example   (10 cols, 4 nets){leOk}");
        Console.WriteLine($"2. Yoshimura Example   (10 cols, 4 nets){yoshOk}");
        Console.WriteLine("3. Custom Channel      (choose size & algorithm)");
        Console.WriteLine("4. Load Channel from File");
        Console.WriteLine("5. Benchmark           (various sizes, both algorithms)");
        Console.WriteLine("6. Channel with Conflicts");
        Console.WriteLine("7. Generate HTML Report  ★" +
                          (leOk != "" || yoshOk != "" ? "  (saved results available)" : "  (run algorithms first)"));
        Console.WriteLine("8. Generate Test Data Files");
        Console.WriteLine("0. Exit");
        Console.Write("\nYour choice: ");
    }

    // ── 1. Left-Edge ─────────────────────────────────────────────────
    private static void RunSimpleExample()
    {
        Console.WriteLine("\n=== Left-Edge Example ===\n");
        var channel = _generator.GenerateSimpleChannel(width: 10, netCount: 4);
        _consoleViz.DisplayChannel(channel);
        PrintVcgInfo(channel);

        RunSaveAndStore(_leftEdge, channel, "simple_left_edge");
    }

    // ── 2. Yoshimura ─────────────────────────────────────────────────
    private static void RunYoshimuraExample()
    {
        Console.WriteLine("\n=== Yoshimura Example ===\n");
        var gen     = new ChannelDataGenerator(seed: 42);
        var channel = gen.GenerateSimpleChannel(width: 10, netCount: 4);
        _consoleViz.DisplayChannel(channel);
        PrintVcgInfo(channel);

        RunSaveAndStore(_yoshimura, channel, "yoshimura_example");
    }

    // ── 3. Custom channel ─────────────────────────────────────────────
    private static void RunCustomChannel()
    {
        Console.WriteLine("\n=== Custom Channel ===\n");

        Console.Write("Channel width:  ");
        if (!int.TryParse(Console.ReadLine(), out int width) || width <= 0)
        { Console.WriteLine("Invalid width!"); return; }

        Console.Write("Number of nets: ");
        if (!int.TryParse(Console.ReadLine(), out int netCount) || netCount <= 0)
        { Console.WriteLine("Invalid net count!"); return; }

        Console.Write("Algorithm? [1] Left-Edge  [2] Yoshimura  [3] Both : ");
        var algoChoice = Console.ReadLine()?.Trim();

        var channel = _generator.GenerateSimpleChannel(width, netCount);
        _consoleViz.DisplayChannel(channel);
        PrintVcgInfo(channel);
        Directory.CreateDirectory("output");

        switch (algoChoice)
        {
            case "2":
                RunSaveAndStore(_yoshimura, channel, "custom_yoshimura");
                break;
            case "3":
                RunSaveAndStore(_leftEdge,  channel, "custom_left_edge");
                RunSaveAndStore(_yoshimura, channel, "custom_yoshimura");
                PrintComparisonFromStore();
                break;
            default:
                RunSaveAndStore(_leftEdge, channel, "custom_left_edge");
                break;
        }
    }

    // ── 4. Load from file ─────────────────────────────────────────────
    private static void RunFromFile()
    {
        Console.WriteLine("\n=== Load from File ===\n");

        Console.Write("File path: ");
        var filePath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        { Console.WriteLine("File not found!"); return; }

        try
        {
            var channel = _fileReader.ReadFromFile(filePath);
            _consoleViz.DisplayChannel(channel);
            PrintVcgInfo(channel);

            Console.Write("Algorithm? [1] Left-Edge  [2] Yoshimura  [3] Both : ");
            var algoChoice = Console.ReadLine()?.Trim();
            Directory.CreateDirectory("output");

            switch (algoChoice)
            {
                case "2":
                    RunSaveAndStore(_yoshimura, channel, "file_yoshimura");
                    break;
                case "3":
                    RunSaveAndStore(_leftEdge,  channel, "file_left_edge");
                    RunSaveAndStore(_yoshimura, channel, "file_yoshimura");
                    PrintComparisonFromStore();
                    break;
                default:
                    RunSaveAndStore(_leftEdge, channel, "file_left_edge");
                    break;
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
    }

    // ── 5. Benchmark ──────────────────────────────────────────────────
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

        PrintBenchmarkTable("Left-Edge", leResults, testCases.Select(t => (t.Width, t.NetCount)).ToArray());
        PrintBenchmarkTable("Yoshimura", yoshResults, testCases.Select(t => (t.Width, t.NetCount)).ToArray());

        Console.WriteLine("\n=== Delta (Yoshimura − Left-Edge) ===");
        Console.WriteLine($"{"Size",-10} | {"ΔTracks",8} | {"ΔWire",10} | {"ΔTime ms",10}");
        Console.WriteLine(new string('-', 50));
        for (int i = 0; i < leResults.Count; i++)
        {
            var lm   = leResults[i].GetMetrics();
            var ym   = yoshResults[i].GetMetrics();
            var size = $"{testCases[i].Width}×{testCases[i].NetCount}";
            Console.WriteLine(
                $"{size,-10} | {ym.TracksUsed - lm.TracksUsed,+8} | " +
                $"{ym.TotalWireLength - lm.TotalWireLength,+10:F0} | " +
                $"{ym.ExecutionTimeMs - lm.ExecutionTimeMs,+10:F2}");
        }
    }

    // ── 6. Conflicts demo ─────────────────────────────────────────────
    private static void RunChannelWithConflicts()
    {
        Console.WriteLine("\n=== Channel with Conflicts ===\n");
        Console.WriteLine("Generating channel with routing conflicts…\n");

        var channel = _generator.GenerateChannelWithConflicts(
            width: 15, netCount: 8, conflictCount: 2);

        _consoleViz.DisplayChannel(channel);
        PrintVcgInfo(channel);
        Directory.CreateDirectory("output");

        RunSaveAndStore(_leftEdge,  channel, "conflicts_left_edge");
        RunSaveAndStore(_yoshimura, channel, "conflicts_yoshimura");
        PrintComparisonFromStore();
    }

    // ── 7. HTML report ────────────────────────────────────────────────
    private static void RunHtmlReport()
    {
        Console.WriteLine("\n=== HTML Report ===\n");

        // Load whatever was saved last — order: Left-Edge, then Yoshimura
        var stored = new List<StoredResult>();

        foreach (var algoName in new[] { _leftEdge.Name, _yoshimura.Name })
        {
            var r = _store.TryLoad(algoName);
            if (r is not null)
            {
                stored.Add(r);
                Console.WriteLine($"  Loaded: {algoName}  " +
                                  $"(saved {r.SavedAt[..16].Replace('T', ' ')})");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  Missing: {algoName} — run it first (menu 1 or 2)");
                Console.ResetColor();
            }
        }

        if (stored.Count == 0)
        {
            Console.WriteLine("\nNo saved results found. Run at least one algorithm first.");
            return;
        }

        // Warn if results are from different channels
        if (stored.Count == 2)
        {
            var c1 = stored[0].Channel;
            var c2 = stored[1].Channel;
            if (c1.Width != c2.Width || c1.Nets != c2.Nets)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    "\n  ⚠  Warning: saved results are from different channels " +
                    $"({c1.Width}×{c1.Nets} vs {c2.Width}×{c2.Nets}).\n" +
                    "     The report will show them side-by-side but comparison may be misleading.");
                Console.ResetColor();
            }
        }

        Directory.CreateDirectory("output");
        var htmlPath = Path.Combine("output", "comparison_report.html");
        _htmlViz.SaveToFile(stored, htmlPath);
        Console.WriteLine($"\nHTML report saved to: {htmlPath}");

        OpenInBrowser(htmlPath);
    }

    // ── 8. Generate test data ─────────────────────────────────────────
    private static void GenerateTestData()
    {
        Console.WriteLine("\n=== Generate Test Data ===\n");
        Directory.CreateDirectory("testdata");

        _fileWriter.WriteToFile(_generator.GenerateSimpleChannel(10, 4),          "testdata/simple_10x4.txt");
        _fileWriter.WriteToFile(_generator.GenerateSimpleChannel(20, 10),         "testdata/simple_20x10.txt");
        _fileWriter.WriteToFile(_generator.GenerateChannelWithConflicts(15,8,2),  "testdata/conflicts_15x8.txt");
        _fileWriter.WriteToFile(_generator.GenerateSimpleChannel(100, 50),        "testdata/large_100x50.txt");

        Console.WriteLine("Test data generated in 'testdata/':");
        Console.WriteLine("  simple_10x4.txt / simple_20x10.txt");
        Console.WriteLine("  conflicts_15x8.txt / large_100x50.txt");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Route → display → save SVG + TXT → store JSON for HTML report.
    /// This is the single place where all persistence happens.
    /// </summary>
    private static void RunSaveAndStore(
        src.Application.Interfaces.IRoutingAlgorithm algo,
        Channel channel,
        string baseName)
    {
        var result  = algo.Route(channel);
        _consoleViz.DisplayRoutingResult(result);

        Directory.CreateDirectory("output");
        var svgPath = Path.Combine("output", baseName + ".svg");
        var txtPath = Path.Combine("output", baseName + "_result.txt");
        _svgViz.SaveToFile(result, svgPath);
        _fileWriter.WriteResultToFile(result, txtPath);

        // ← key step: persist JSON so RunHtmlReport() can read it later
        _store.Save(result);

        Console.WriteLine($"Saved: {svgPath}  |  {txtPath}");
        Console.WriteLine($"JSON:  {_store.FilePath(result.AlgorithmName)}");
    }

    /// <summary>
    /// Prints a quick console comparison using whatever is currently stored.
    /// </summary>
    private static void PrintComparisonFromStore()
    {
        var le   = _store.TryLoad(_leftEdge.Name);
        var yosh = _store.TryLoad(_yoshimura.Name);
        if (le is null || yosh is null) return;

        Console.WriteLine();
        Console.WriteLine($"{"Algorithm",-22} | {"Tracks",6} | {"Wire",10} | {"Conflicts",9} | {"Time ms",10}");
        Console.WriteLine(new string('-', 70));
        PrintStoredRow(le);
        PrintStoredRow(yosh);
        Console.WriteLine();
    }

    private static void PrintStoredRow(StoredResult r)
    {
        var d = r.Result;
        Console.Write($"{r.AlgorithmName,-22} | {d.TracksUsed,6} | {d.WireLength,10:F0} | ");
        if (d.ConflictCount > 0) { Console.ForegroundColor = ConsoleColor.Red; }
        else                     { Console.ForegroundColor = ConsoleColor.Green; }
        Console.Write($"{d.ConflictCount,9}");
        Console.ResetColor();
        Console.WriteLine($" | {d.ExecutionMs,10:F2}");
    }

    private static void PrintVcgInfo(Channel channel)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("── Vertical Constraint Graph (VCG) ──");
        bool any = false;
        for (int col = 0; col < channel.Width; col++)
        {
            int top = channel.TopRow[col], bot = channel.BottomRow[col];
            if (top != 0 && bot != 0 && top != bot)
            {
                Console.WriteLine($"  Net {top,2} must be ABOVE Net {bot,2}  (column {col})");
                any = true;
            }
        }
        if (!any) Console.WriteLine("  (no vertical constraints)");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintBenchmarkTable(
        string label, List<RoutingResult> results, (int Width, int NetCount)[] cases)
    {
        Console.WriteLine($"\n=== {label} ===");
        Console.WriteLine($"{"Size",-10} | {"Tracks",6} | {"Wire",10} | {"Time ms",10}");
        Console.WriteLine(new string('-', 46));
        for (int i = 0; i < results.Count; i++)
        {
            var m    = results[i].GetMetrics();
            var size = $"{cases[i].Width}×{cases[i].NetCount}";
            Console.WriteLine($"{size,-10} | {m.TracksUsed,6} | {m.TotalWireLength,10:F0} | {m.ExecutionTimeMs,10:F2}");
        }
    }

    private static void OpenInBrowser(string filePath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = Path.GetFullPath(filePath),
                UseShellExecute = true,
            });
        }
        catch
        {
            Console.WriteLine("(Could not open browser automatically — open the file manually)");
        }
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