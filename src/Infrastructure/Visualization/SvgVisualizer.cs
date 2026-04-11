using src.Domain.Entities;
using System.Text;

namespace src.Infrastructure.Visualization;

/// <summary>
/// SVG-based visualizer for generating graphical channel routing diagrams
/// </summary>
public class SvgVisualizer
{
    private const int CellWidth = 40;
    private const int CellHeight = 30;
    private const int ContactRadius = 8;
    private const int MarginX = 50;
    private const int MarginY = 50;

    private static readonly string[] NetColors = new[]
    {
        "#00CED1", "#FFD700", "#32CD32", "#FF69B4", "#4169E1", "#FF4500",
        "#9370DB", "#20B2AA", "#FFA500", "#BA55D3", "#00BFFF", "#DC143C",
        "#7FFF00", "#FF1493", "#00FA9A", "#FF6347", "#4682B4", "#FF8C00"
    };

    public string GenerateSvg(RoutingResult result)
    {
        var channel = result.Channel;
        var width = channel.Width * CellWidth + 2 * MarginX;
        var height = (result.TracksUsed + 3) * CellHeight + 2 * MarginY;

        var svg = new StringBuilder();
        svg.AppendLine($"<svg width=\"{width}\" height=\"{height}\" xmlns=\"http://www.w3.org/2000/svg\">");
        svg.AppendLine("  <style>");
        svg.AppendLine("    text { font-family: Arial, sans-serif; font-size: 12px; }");
        svg.AppendLine("    .track-label { font-size: 14px; font-weight: bold; }");
        svg.AppendLine("    .net-label { font-size: 10px; fill: white; }");
        svg.AppendLine("  </style>");

        // White background
        svg.AppendLine($"  <rect width=\"{width}\" height=\"{height}\" fill=\"white\"/>");

        // Draw grid
        DrawGrid(svg, channel.Width, result.TracksUsed);

        // Draw track labels
        DrawTrackLabels(svg, result.TracksUsed);

        // Draw horizontal segments
        DrawHorizontalSegments(svg, result);

        // Draw vertical segments
        DrawVerticalSegments(svg, result);

        // Draw contacts
        DrawContacts(svg, channel);

        // Draw legend
        DrawLegend(svg, result, width);

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private void DrawGrid(StringBuilder svg, int columns, int tracks)
    {
        // Vertical lines
        for (int col = 0; col <= columns; col++)
        {
            int x = MarginX + col * CellWidth;
            int y1 = MarginY;
            int y2 = MarginY + (tracks + 2) * CellHeight;
            svg.AppendLine($"  <line x1=\"{x}\" y1=\"{y1}\" x2=\"{x}\" y2=\"{y2}\" stroke=\"#E0E0E0\" stroke-width=\"1\"/>");
        }

        // Horizontal lines
        for (int track = 0; track <= tracks + 2; track++)
        {
            int y = MarginY + track * CellHeight;
            int x1 = MarginX;
            int x2 = MarginX + columns * CellWidth;
            svg.AppendLine($"  <line x1=\"{x1}\" y1=\"{y}\" x2=\"{x2}\" y2=\"{y}\" stroke=\"#E0E0E0\" stroke-width=\"1\"/>");
        }
    }

    private void DrawTrackLabels(StringBuilder svg, int tracks)
    {
        // Top label
        int y = MarginY + CellHeight / 2 + 5;
        svg.AppendLine($"  <text x=\"{MarginX - 30}\" y=\"{y}\" class=\"track-label\">T</text>");

        // Track labels
        for (int track = 0; track < tracks; track++)
        {
            y = MarginY + (track + 1) * CellHeight + CellHeight / 2 + 5;
            svg.AppendLine($"  <text x=\"{MarginX - 30}\" y=\"{y}\" class=\"track-label\">{track}</text>");
        }

        // Bottom label
        y = MarginY + (tracks + 2) * CellHeight - CellHeight / 2 + 5;
        svg.AppendLine($"  <text x=\"{MarginX - 30}\" y=\"{y}\" class=\"track-label\">B</text>");
    }

    private void DrawHorizontalSegments(StringBuilder svg, RoutingResult result)
    {
        var horizontalSegments = result.AllSegments
            .Where(s => s.Type == SegmentType.Horizontal)
            .ToList();

        foreach (var segment in horizontalSegments)
        {
            var color = GetNetColor(segment.NetId);
            int y = MarginY + (segment.Track + 1) * CellHeight + CellHeight / 2;
            int x1 = MarginX + segment.StartColumn * CellWidth + CellWidth / 2;
            int x2 = MarginX + segment.EndColumn * CellWidth + CellWidth / 2;

            // Draw thick colored line
            svg.AppendLine($"  <line x1=\"{x1}\" y1=\"{y}\" x2=\"{x2}\" y2=\"{y}\" " +
                          $"stroke=\"{color}\" stroke-width=\"6\" stroke-linecap=\"round\"/>");

            // Draw net ID label
            int textX = (x1 + x2) / 2;
            svg.AppendLine($"  <text x=\"{textX}\" y=\"{y - 10}\" class=\"net-label\" " +
                          $"text-anchor=\"middle\" fill=\"{color}\">{segment.NetId}</text>");
        }
    }

    private void DrawVerticalSegments(StringBuilder svg, RoutingResult result)
    {
        var verticalSegments = result.AllSegments
            .Where(s => s.Type == SegmentType.Vertical)
            .ToList();

        foreach (var segment in verticalSegments)
        {
            var color = GetNetColor(segment.NetId);
            int x = MarginX + segment.StartColumn * CellWidth + CellWidth / 2;
            int y1 = MarginY + CellHeight / 2;  // Top contact
            int y2 = MarginY + (segment.Track + 1) * CellHeight + CellHeight / 2;  // Track
            int y3 = MarginY + (result.TracksUsed + 2) * CellHeight - CellHeight / 2;  // Bottom contact

            // Connect top to track
            svg.AppendLine($"  <line x1=\"{x}\" y1=\"{y1}\" x2=\"{x}\" y2=\"{y2}\" " +
                          $"stroke=\"{color}\" stroke-width=\"3\" stroke-dasharray=\"5,3\"/>");

            // Connect track to bottom
            svg.AppendLine($"  <line x1=\"{x}\" y1=\"{y2}\" x2=\"{x}\" y2=\"{y3}\" " +
                          $"stroke=\"{color}\" stroke-width=\"3\" stroke-dasharray=\"5,3\"/>");
        }
    }

    private void DrawContacts(StringBuilder svg, Channel channel)
    {
        // Top contacts
        for (int col = 0; col < channel.Width; col++)
        {
            if (channel.TopRow[col] != 0)
            {
                int netId = channel.TopRow[col];
                var color = GetNetColor(netId);
                int x = MarginX + col * CellWidth + CellWidth / 2;
                int y = MarginY + CellHeight / 2;

                svg.AppendLine($"  <circle cx=\"{x}\" cy=\"{y}\" r=\"{ContactRadius}\" " +
                              $"fill=\"{color}\" stroke=\"black\" stroke-width=\"2\"/>");
                svg.AppendLine($"  <text x=\"{x}\" y=\"{y + 4}\" class=\"net-label\" " +
                              $"text-anchor=\"middle\">{netId}</text>");
            }
        }

        // Bottom contacts
        int bottomY = MarginY + (channel.Nets.Count + 2) * CellHeight - CellHeight / 2;
        for (int col = 0; col < channel.Width; col++)
        {
            if (channel.BottomRow[col] != 0)
            {
                int netId = channel.BottomRow[col];
                var color = GetNetColor(netId);
                int x = MarginX + col * CellWidth + CellWidth / 2;

                svg.AppendLine($"  <circle cx=\"{x}\" cy=\"{bottomY}\" r=\"{ContactRadius}\" " +
                              $"fill=\"{color}\" stroke=\"black\" stroke-width=\"2\"/>");
                svg.AppendLine($"  <text x=\"{x}\" y=\"{bottomY + 4}\" class=\"net-label\" " +
                              $"text-anchor=\"middle\">{netId}</text>");
            }
        }
    }

    private void DrawLegend(StringBuilder svg, RoutingResult result, int totalWidth)
    {
        int legendY = 20;
        svg.AppendLine($"  <text x=\"{totalWidth / 2}\" y=\"{legendY}\" " +
                      $"text-anchor=\"middle\" style=\"font-size: 18px; font-weight: bold;\">" +
                      $"{result.AlgorithmName}</text>");

        legendY += 20;
        svg.AppendLine($"  <text x=\"{totalWidth / 2}\" y=\"{legendY}\" text-anchor=\"middle\">" +
                      $"Tracks: {result.TracksUsed} | Wire Length: {result.TotalWireLength:F0} | " +
                      $"Time: {result.ExecutionTime.TotalMilliseconds:F2}ms</text>");
    }

    private string GetNetColor(int netId)
    {
        return NetColors[(netId - 1) % NetColors.Length];
    }

    public void SaveToFile(RoutingResult result, string filePath)
    {
        var svg = GenerateSvg(result);
        File.WriteAllText(filePath, svg);
    }
}