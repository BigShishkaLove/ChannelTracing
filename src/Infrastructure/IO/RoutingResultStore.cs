using src.Domain.Entities;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace src.Infrastructure.IO;

/// <summary>
/// Persists routing results to JSON so the HTML report can be generated
/// independently from algorithm execution.
///
/// Each algorithm writes to:   output/{slug}_latest.json
/// HtmlVisualizer reads from:  output/left_edge_latest.json
///                             output/yoshimura_latest.json
/// </summary>
public class RoutingResultStore
{
    private readonly string _outputDir;

    public RoutingResultStore(string outputDir = "output")
    {
        _outputDir = outputDir;
    }

    // ── Slug helpers ────────────────────────────────────────────────

    public static string Slug(string algorithmName) =>
        algorithmName.ToLower()
                     .Replace(" ", "_")
                     .Replace("-", "_")
                     .Replace("(", "")
                     .Replace(")", "");

    public string FilePath(string algorithmName) =>
        Path.Combine(_outputDir, $"{Slug(algorithmName)}_latest.json");

    // ── Save ────────────────────────────────────────────────────────

    public void Save(RoutingResult result)
    {
        Directory.CreateDirectory(_outputDir);
        var json = Serialize(result);
        File.WriteAllText(FilePath(result.AlgorithmName), json, Encoding.UTF8);
    }

    // ── Load ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns null if the file doesn't exist yet.
    /// Throws on malformed JSON.
    /// </summary>
    public StoredResult? TryLoad(string algorithmName)
    {
        var path = FilePath(algorithmName);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<StoredResult>(
            File.ReadAllText(path, Encoding.UTF8),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public bool Exists(string algorithmName) =>
        File.Exists(FilePath(algorithmName));

    // ── Serialization ───────────────────────────────────────────────

    private static string Serialize(RoutingResult r)
    {
        var channel = r.Channel;

        var obj = new
        {
            algorithmName   = r.AlgorithmName,
            savedAt         = DateTime.Now.ToString("o"),
            channel = new
            {
                width     = channel.Width,
                nets      = channel.Nets.Count,
                topRow    = channel.TopRow,
                bottomRow = channel.BottomRow,
            },
            result = new
            {
                tracksUsed    = r.TracksUsed,
                wireLength    = r.TotalWireLength,
                conflictCount = r.ConflictDescriptions.Count,
                executionMs   = r.ExecutionTime.TotalMilliseconds,
                segments      = r.AllSegments.Select(s => new
                {
                    netId = s.NetId,
                    type  = s.Type == SegmentType.Horizontal ? 0 : 1,
                    start = s.StartColumn,
                    end   = s.EndColumn,
                    track = s.Track,
                }),
            },
        };

        return JsonSerializer.Serialize(obj,
            new JsonSerializerOptions { WriteIndented = false });
    }
}

// ── DTO read back from disk ──────────────────────────────────────────

public record StoredResult(
    string            AlgorithmName,
    string            SavedAt,
    StoredChannel     Channel,
    StoredResultData  Result);

public record StoredChannel(
    int   Width,
    int   Nets,
    int[] TopRow,
    int[] BottomRow);

public record StoredResultData(
    int                  TracksUsed,
    double               WireLength,
    int                  ConflictCount,
    double               ExecutionMs,
    List<StoredSegment>  Segments);

public record StoredSegment(
    int NetId,
    int Type,   // 0 = Horizontal, 1 = Vertical
    int Start,
    int End,
    int Track);
