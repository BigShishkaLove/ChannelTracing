using src.Domain.Entities;

namespace src.Infrastructure.IO;

public class ChannelFileReader
{
    public Channel ReadFromFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        if (lines.Length < 3) throw new InvalidDataException("File must contain at least 3 lines: width, top row, bottom row");

        if (!int.TryParse(lines[0].Trim(), out var width) || width <= 0)
            throw new InvalidDataException($"Invalid width value: {lines[0]}");

        var topRow = ParseRow(lines[1], width, "top");
        var bottomRow = ParseRow(lines[2], width, "bottom");
        return new Channel(width, topRow, bottomRow);
    }

    private static int[] ParseRow(string line, int expectedLength, string rowName)
    {
        var parts = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != expectedLength)
            throw new InvalidDataException($"{rowName} row has {parts.Length} values, expected {expectedLength}");

        var row = new int[expectedLength];
        for (var i = 0; i < expectedLength; i++)
        {
            if (!int.TryParse(parts[i], out row[i]) || row[i] < 0)
                throw new InvalidDataException($"Invalid value in {rowName} row at position {i}: {parts[i]}");
        }
        return row;
    }
}
