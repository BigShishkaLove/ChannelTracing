using src.Domain.Entities;
using src.Infrastructure.IO;
using System.Text;
using System.Text.Json;

namespace src.Infrastructure.Visualization;

/// <summary>
/// Generates an interactive HTML report.
/// Can consume either live RoutingResult objects or StoredResult DTOs
/// loaded from disk by RoutingResultStore.
/// </summary>
public class HtmlVisualizer
{
    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Generate from live results (e.g. benchmark mode).</summary>
    public void SaveToFile(IEnumerable<RoutingResult> results, string filePath)
    {
        var list = results.ToList();
        var json = BuildJsonFromLive(list[0].Channel, list);
        WriteHtml(json, filePath);
    }

    /// <summary>Generate from stored results loaded off disk.</summary>
    public void SaveToFile(IEnumerable<StoredResult> results, string filePath)
    {
        var list = results.ToList();
        var json = BuildJsonFromStored(list);
        WriteHtml(json, filePath);
    }

    // ── JSON: live RoutingResult ──────────────────────────────────────

    private static string BuildJsonFromLive(Channel channel, List<RoutingResult> results)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"width\":{channel.Width},");
        sb.Append($"\"nets\":{channel.Nets.Count},");
        AppendRow(sb, "topRow",    channel.TopRow,    channel.Width); sb.Append(',');
        AppendRow(sb, "bottomRow", channel.BottomRow, channel.Width); sb.Append(',');
        sb.Append("\"results\":[");
        for (int i = 0; i < results.Count; i++)
        {
            if (i > 0) sb.Append(',');
            AppendLiveResult(sb, results[i]);
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static void AppendLiveResult(StringBuilder sb, RoutingResult r)
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
            sb.Append($"{{\"netId\":{s.NetId},\"type\":{(s.Type == SegmentType.Horizontal ? 0 : 1)}," +
                      $"\"start\":{s.StartColumn},\"end\":{s.EndColumn},\"track\":{s.Track}}}");
        }
        sb.Append("]}");
    }

    // ── JSON: StoredResult from disk ──────────────────────────────────

    private static string BuildJsonFromStored(List<StoredResult> results)
    {
        // All results share the same channel (same input file was used)
        var ch = results[0].Channel;

        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"width\":{ch.Width},");
        sb.Append($"\"nets\":{ch.Nets},");
        AppendRow(sb, "topRow",    ch.TopRow,    ch.Width); sb.Append(',');
        AppendRow(sb, "bottomRow", ch.BottomRow, ch.Width); sb.Append(',');
        sb.Append("\"results\":[");
        for (int i = 0; i < results.Count; i++)
        {
            if (i > 0) sb.Append(',');
            AppendStoredResult(sb, results[i]);
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static void AppendStoredResult(StringBuilder sb, StoredResult r)
    {
        var d = r.Result;
        sb.Append('{');
        sb.Append($"\"algorithmName\":{JsonSerializer.Serialize(r.AlgorithmName)},");
        sb.Append($"\"tracksUsed\":{d.TracksUsed},");
        sb.Append($"\"wireLength\":{d.WireLength:F0},");
        sb.Append($"\"conflictCount\":{d.ConflictCount},");
        sb.Append($"\"executionMs\":{d.ExecutionMs:F3},");
        sb.Append("\"segments\":[");
        for (int i = 0; i < d.Segments.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var s = d.Segments[i];
            sb.Append($"{{\"netId\":{s.NetId},\"type\":{s.Type}," +
                      $"\"start\":{s.Start},\"end\":{s.End},\"track\":{s.Track}}}");
        }
        sb.Append("]}");
    }

    // ── Shared helpers ────────────────────────────────────────────────

    private static void AppendRow(StringBuilder sb, string key, int[] row, int width)
    {
        sb.Append($"\"{key}\":");
        if (width <= 50_000)
        {
            sb.Append('['); sb.Append(string.Join(",", row)); sb.Append(']');
        }
        else
        {
            // Sparse for very wide channels
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

    private static void WriteHtml(string json, string filePath)
    {
        var template = LoadTemplate();
        var html     = template.Replace("__ROUTING_DATA__", json);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, html, Encoding.UTF8);
    }

    private static string LoadTemplate()
    {
        var paths = new[]
        {
            "Infrastructure/Visualization/channel_routing_viewer.html",
            "channel_routing_viewer.html",
        };
        foreach (var p in paths)
            if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8);

        throw new FileNotFoundException(
            "HTML template not found. Place 'channel_routing_viewer.html' " +
            "next to the executable or in Infrastructure/Visualization/.");
    }
}