using src.Domain.Entities;

namespace src.Infrastructure.IO;

/// <summary>
/// Service for reading channel data from files
/// </summary>
public class ChannelFileReader
{
    public Channel ReadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Channel file not found: {filePath}");

        var lines = File.ReadAllLines(filePath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length < 3)
            throw new InvalidDataException("File must contain at least 3 lines: width, top row, bottom row");

        // Parse width
        if (!int.TryParse(lines[0].Trim(), out int width) || width <= 0)
            throw new InvalidDataException($"Invalid width value: {lines[0]}");

        // Parse top row
        var topRow = ParseRow(lines[1], width, "top");

        // Parse bottom row
        var bottomRow = ParseRow(lines[2], width, "bottom");

        return new Channel(width, topRow, bottomRow);
    }

    public Channel ReadFromString(string data)
    {
        var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length < 3)
            throw new InvalidDataException("Data must contain at least 3 lines");

        if (!int.TryParse(lines[0].Trim(), out int width) || width <= 0)
            throw new InvalidDataException($"Invalid width value: {lines[0]}");

        var topRow = ParseRow(lines[1], width, "top");
        var bottomRow = ParseRow(lines[2], width, "bottom");

        return new Channel(width, topRow, bottomRow);
    }

    private int[] ParseRow(string line, int expectedLength, string rowName)
    {
        var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != expectedLength)
            throw new InvalidDataException(
                $"{rowName} row has {parts.Length} values, expected {expectedLength}");

        var row = new int[expectedLength];
        for (int i = 0; i < expectedLength; i++)
        {
            if (!int.TryParse(parts[i], out row[i]) || row[i] < 0)
                throw new InvalidDataException(
                    $"Invalid value in {rowName} row at position {i}: {parts[i]}");
        }

        return row;
    }
}

/// <summary>
/// Service for writing channel data to files
/// </summary>
public class ChannelFileWriter
{
    public void WriteToFile(Channel channel, string filePath)
    {
        var lines = new List<string>
        {
            channel.Width.ToString(),
            string.Join(" ", channel.TopRow),
            string.Join(" ", channel.BottomRow)
        };

        File.WriteAllLines(filePath, lines);
    }

    public string WriteToString(Channel channel)
    {
        return $"{channel.Width}\n" +
               $"{string.Join(" ", channel.TopRow)}\n" +
               $"{string.Join(" ", channel.BottomRow)}";
    }

    public void WriteResultToFile(RoutingResult result, string filePath)
    {
        var lines = new List<string>
        {
            $"=== Routing Result: {result.AlgorithmName} ===",
            $"Execution Time: {result.ExecutionTime.TotalMilliseconds:F2} ms",
            $"Tracks Used: {result.TracksUsed}",
            $"Total Wire Length: {result.TotalWireLength:F0}",
            $"Conflicts: {(result.HasConflicts ? $"Yes ({result.ConflictDescriptions.Count})" : "No")}",
            ""
        };

        if (result.HasConflicts)
        {
            lines.Add("Conflict Details:");
            lines.AddRange(result.ConflictDescriptions.Select(c => $"  - {c}"));
            lines.Add("");
        }

        lines.Add("Segments:");
        foreach (var segment in result.AllSegments.OrderBy(s => s.Track).ThenBy(s => s.StartColumn))
        {
            lines.Add($"  {segment}");
        }

        lines.Add("");
        lines.Add("Net Assignments:");
        foreach (var net in result.Channel.Nets.Values.OrderBy(n => n.Id))
        {
            lines.Add($"  {net}");
        }

        File.WriteAllLines(filePath, lines);
    }
}