using src.Domain.Entities;

namespace src.Infrastructure.IO;

public class ChannelFileWriter
{
    public void WriteToFile(Channel channel, string filePath)
    {
        File.WriteAllLines(filePath, [
            channel.Width.ToString(),
            string.Join(" ", channel.TopRow),
            string.Join(" ", channel.BottomRow)
        ]);
    }

    public void WriteResultToFile(RoutingResult result, string filePath)
    {
        var lines = new List<string>
        {
            $"algorithm={result.AlgorithmName}",
            $"tracks={result.TracksUsed}",
            $"wire_length={result.TotalWireLength:F0}",
            $"execution_ms={result.ExecutionTime.TotalMilliseconds:F3}",
            $"conflicts={result.ConflictDescriptions.Count}"
        };

        lines.AddRange(result.ConflictDescriptions.Select(c => $"conflict={c}"));
        lines.Add("segments:");
        lines.AddRange(result.AllSegments.Select(s =>
            $"net={s.NetId};type={s.Type};start={s.StartColumn};end={s.EndColumn};track={s.Track}"));

        File.WriteAllLines(filePath, lines);
    }
}
