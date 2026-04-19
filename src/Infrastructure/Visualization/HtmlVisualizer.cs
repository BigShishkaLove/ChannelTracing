using src.Domain.Entities;
using System.Text;
using System.Text.Json;

namespace src.Infrastructure.Visualization;

/// <summary>
/// Generates an interactive HTML report with Canvas-based zoom/pan viewer.
/// Supports multiple algorithm results for comparison.
/// Handles channels with millions of columns efficiently via virtual rendering.
/// </summary>
public class HtmlVisualizer
{

    public void SaveToFile(RoutingResult result, string filePath)
        => SaveToFile(new[] { result }, filePath);

    public void SaveToFile(IEnumerable<RoutingResult> results, string filePath)
    {
        var html = Generate(results);
        File.WriteAllText(filePath, html, Encoding.UTF8);
    }

    public string Generate(IEnumerable<RoutingResult> results)
    {
        var list    = results.ToList();
        var channel = list[0].Channel;
        var json    = BuildJson(channel, list);
        var html    = LoadTemplate().Replace("__ROUTING_DATA__", json);
        return html;
    }


    private static string BuildJson(Channel channel, List<RoutingResult> results)
    {

        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"width\":{channel.Width},");
        sb.Append($"\"nets\":{channel.Nets.Count},");

        sb.Append("\"topRow\":");
        AppendSparseRow(sb, channel.TopRow, channel.Width);
        sb.Append(',');

        sb.Append("\"bottomRow\":");
        AppendSparseRow(sb, channel.BottomRow, channel.Width);
        sb.Append(',');

        sb.Append("\"results\":[");
        for (int i = 0; i < results.Count; i++)
        {
            if (i > 0) sb.Append(',');
            AppendResult(sb, results[i]);
        }
        sb.Append(']');
        sb.Append('}');

        return sb.ToString();
    }

    private static void AppendSparseRow(StringBuilder sb, int[] row, int width)
    {
        if (width <= 50_000)
        {
            sb.Append('[');
            sb.Append(string.Join(",", row));
            sb.Append(']');
        }
        else
        {
            sb.Append('{');
            bool first = true;
            for (int c = 0; c < width; c++)
            {
                if (row[c] == 0) continue;
                if (!first) sb.Append(',');
                sb.Append($"\"{c}\":{row[c]}");
                first = false;
            }
            sb.Append('}');
        }
    }

    private static void AppendResult(StringBuilder sb, RoutingResult r)
    {
        sb.Append('{');
        sb.Append($"\"algorithmName\":{JsonSerializer.Serialize(r.AlgorithmName)},");
        sb.Append($"\"tracksUsed\":{r.TracksUsed},");
        sb.Append($"\"wireLength\":{r.TotalWireLength:F0},");
        sb.Append($"\"conflictCount\":{r.ConflictDescriptions.Count},");
        sb.Append($"\"executionMs\":{r.ExecutionTime.TotalMilliseconds:F3},");

        sb.Append("\"segments\":[");
        var segs = r.AllSegments;
        for (int i = 0; i < segs.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var s = segs[i];
            // {netId, type, start, end, track}
            sb.Append($"{{\"netId\":{s.NetId}," +
                       $"\"type\":{(s.Type == SegmentType.Horizontal ? 0 : 1)}," +
                       $"\"start\":{s.StartColumn}," +
                       $"\"end\":{s.EndColumn}," +
                       $"\"track\":{s.Track}}}");
        }
        sb.Append(']');
        sb.Append('}');
    }

    private static string LoadTemplate()
    {
        var templatePaths = new[]
        {
            "Infrastructure/Visualization/channel_routing_viewer.html",
            "channel_routing_viewer.html",
        };

        foreach (var path in templatePaths)
        {
            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        return GetEmbeddedTemplate();
    }

    /// <summary>
    /// Returns the full HTML template as a string.
    /// Copy the content of channel_routing_viewer.html here for a self-contained build.
    /// </summary>
    private static string GetEmbeddedTemplate()
    {
        throw new FileNotFoundException(
            "HTML template not found. Place 'channel_routing_viewer.html' next to the executable, " +
            "or embed it in GetEmbeddedTemplate().");
    }
}
