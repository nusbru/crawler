using System.Text;
using System.Text.Json;

namespace CrawlerCli;

/// <summary>
/// Represents a crawl edge (source -> target relationship).
/// </summary>
internal sealed record CrawlEdge(Uri Source, Uri Target);

/// <summary>
/// Handles output formatting and export for crawl results.
/// </summary>
internal sealed class OutputWriter
{
    private readonly List<CrawlEdge> _edges = [];
    private readonly object _lock = new();

    /// <summary>
    /// Records a crawl edge for default output rendering and optional export.
    /// </summary>
    public void WriteEdge(Uri source, Uri target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var edge = new CrawlEdge(source, target);

        // Store for default output and export
        lock (_lock)
        {
            _edges.Add(edge);
        }
    }

    /// <summary>
    /// Writes default crawl output grouped by source URL.
    /// </summary>
    public void WriteGroupedOutput()
    {
        var output = Console.Out;

        List<CrawlEdge> edgesToOutput;
        lock (_lock)
        {
            edgesToOutput = [.. _edges];
        }

        var groupedEdges = edgesToOutput
            .GroupBy(edge => edge.Source)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal);

        foreach (var group in groupedEdges)
        {
            var targets = group
                .Select(edge => edge.Target.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(target => target, StringComparer.Ordinal);

            output.WriteLine($"{group.Key} -> {string.Join(", ", targets)}");
        }
    }

    /// <summary>
    /// Exports collected edges to a file in the specified format.
    /// </summary>
    public async Task ExportAsync(string outputPath, string format, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        List<CrawlEdge> edgesToExport;
        lock (_lock)
        {
            edgesToExport = [.. _edges];
        }

        var content = format.ToLowerInvariant() switch
        {
            "json" => GenerateJson(edgesToExport),
            "html" => GenerateHtml(edgesToExport),
            "csv" => GenerateCsv(edgesToExport),
            _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format))
        };

        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
    }

    /// <summary>
    /// Groups edges by source URL with deterministic ordering.
    /// </summary>
    private static IEnumerable<(string SourceUrl, List<string> Targets)> GroupEdges(List<CrawlEdge> edges)
    {
        return edges
            .GroupBy(edge => edge.Source)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group => (
                SourceUrl: group.Key.ToString(),
                Targets: group
                    .Select(edge => edge.Target.ToString())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(target => target, StringComparer.Ordinal)
                    .ToList()
            ));
    }

    private static string GenerateJson(List<CrawlEdge> edges)
    {
        var data = GroupEdges(edges).Select(g => new
        {
            SourceUrl = g.SourceUrl,
            Target = g.Targets
        }).ToList();

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GenerateHtml(List<CrawlEdge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"utf-8\">");
        sb.AppendLine("    <title>Crawl Results</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("        table { border-collapse: collapse; width: 100%; }");
        sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("        th { background-color: #4CAF50; color: white; }");
        sb.AppendLine("        tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>Crawl Results</h1>");
        sb.AppendLine("    <table>");
        sb.AppendLine("        <tr><th>SourceUrl</th><th>Target</th></tr>");

        foreach (var (sourceUrl, targets) in GroupEdges(edges))
        {
            var encodedSource = System.Net.WebUtility.HtmlEncode(sourceUrl);

            for (var i = 0; i < targets.Count; i++)
            {
                var encodedTarget = System.Net.WebUtility.HtmlEncode(targets[i]);

                if (i == 0)
                {
                    sb.AppendLine($"        <tr><td rowspan=\"{targets.Count}\">{encodedSource}</td><td>{encodedTarget}</td></tr>");
                }
                else
                {
                    sb.AppendLine($"        <tr><td>{encodedTarget}</td></tr>");
                }
            }
        }

        sb.AppendLine("    </table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GenerateCsv(List<CrawlEdge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SourceUrl,Target");

        foreach (var (sourceUrl, targets) in GroupEdges(edges))
        {
            foreach (var target in targets)
            {
                sb.AppendLine($"\"{sourceUrl}\",\"{target}\"");
            }
        }

        return sb.ToString();
    }
}
