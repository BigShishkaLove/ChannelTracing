using src.Domain.Entities;
using System.Text;

namespace src.Infrastructure.Visualization;

/// <summary>
/// Console-based visualizer for channel routing results
/// </summary>
public class ConsoleVisualizer
{
    private static readonly ConsoleColor[] NetColors = new[]
    {
        ConsoleColor.Cyan,
        ConsoleColor.Yellow,
        ConsoleColor.Green,
        ConsoleColor.Magenta,
        ConsoleColor.Blue,
        ConsoleColor.Red,
        ConsoleColor.DarkCyan,
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkGreen,
        ConsoleColor.DarkMagenta,
        ConsoleColor.DarkBlue,
        ConsoleColor.DarkRed
    };

    public void DisplayChannel(Channel channel)
    {
        Console.WriteLine($"\n=== Channel (Width: {channel.Width}, Nets: {channel.Nets.Count}) ===\n");

        Console.Write("Top:    ");
        for (int i = 0; i < channel.Width; i++)
        {
            DisplayContact(channel.TopRow[i]);
        }
        Console.WriteLine();

        Console.Write("Bottom: ");
        for (int i = 0; i < channel.Width; i++)
        {
            DisplayContact(channel.BottomRow[i]);
        }
        Console.WriteLine("\n");
    }

    public void DisplayRoutingResult(RoutingResult result)
    {
        Console.WriteLine($"\n=== {result.AlgorithmName} - Routing Result ===\n");
        Console.WriteLine(result.ToString());
        Console.WriteLine();

        if (result.HasConflicts)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Conflicts detected:");
            foreach (var conflict in result.ConflictDescriptions)
            {
                Console.WriteLine($"  ! {conflict}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        DisplayRoutedChannel(result);
    }

    private void DisplayRoutedChannel(RoutingResult result)
    {
        var channel = result.Channel;
        var tracksUsed = result.TracksUsed;

        Console.WriteLine("Channel Visualization:");
        Console.WriteLine();

        // Display top contacts
        Console.Write("T: ");
        for (int col = 0; col < channel.Width; col++)
        {
            DisplayContact(channel.TopRow[col]);
        }
        Console.WriteLine();

        // Display each track
        for (int track = 0; track < tracksUsed; track++)
        {
            Console.Write($"{track,2}: ");

            var trackSegments = result.AllSegments
                .Where(s => s.Track == track && s.Type == SegmentType.Horizontal)
                .ToList();

            for (int col = 0; col < channel.Width; col++)
            {
                var segment = trackSegments.FirstOrDefault(s =>
                    s.StartColumn <= col && col <= s.EndColumn);

                if (segment != null)
                {
                    var color = GetNetColor(segment.NetId);
                    Console.ForegroundColor = color;
                    Console.Write($"{segment.NetId,3}");
                    Console.ResetColor();
                }
                else
                {
                    Console.Write("  .");
                }
            }
            Console.WriteLine();
        }

        // Display bottom contacts
        Console.Write("B: ");
        for (int col = 0; col < channel.Width; col++)
        {
            DisplayContact(channel.BottomRow[col]);
        }
        Console.WriteLine("\n");
    }

    private void DisplayContact(int netId)
    {
        if (netId == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  .");
        }
        else
        {
            var color = GetNetColor(netId);
            Console.ForegroundColor = color;
            Console.Write($"{netId,3}");
        }
        Console.ResetColor();
    }

    private ConsoleColor GetNetColor(int netId)
    {
        return NetColors[(netId - 1) % NetColors.Length];
    }

    public void DisplayComparison(List<RoutingResult> results)
    {
        Console.WriteLine("\n=== Algorithm Comparison ===\n");
        Console.WriteLine($"{"Algorithm",-20} | {"Tracks",6} | {"Wire Length",11} | {"Conflicts",9} | {"Time (ms)",10}");
        Console.WriteLine(new string('-', 70));

        foreach (var result in results)
        {
            var metrics = result.GetMetrics();
            Console.Write($"{metrics.AlgorithmName,-20} | ");
            Console.Write($"{metrics.TracksUsed,6} | ");
            Console.Write($"{metrics.TotalWireLength,11:F0} | ");

            if (metrics.HasConflicts)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"{metrics.ConflictCount,9} | ");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{"None",9} | ");
                Console.ResetColor();
            }

            Console.WriteLine($"{metrics.ExecutionTimeMs,10:F2}");
        }
        Console.WriteLine();
    }
}