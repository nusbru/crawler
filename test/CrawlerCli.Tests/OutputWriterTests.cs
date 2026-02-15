using System.Text.Json;

namespace CrawlerCli.Tests;

public class OutputWriterTests
{
    [Fact]
    public void WriteGroupedOutput_GroupsTargetsBySource()
    {
        var writer = new OutputWriter();
        var source = new Uri("https://example.com/page1");
        var target = new Uri("https://example.com/page2");
        var target2 = new Uri("https://example.com/page3");
        var originalOut = Console.Out;

        try
        {
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            writer.WriteEdge(source, target);
            writer.WriteEdge(source, target2);
            writer.WriteGroupedOutput();

            var output = consoleOutput.ToString();
            Assert.Contains("https://example.com/page1 -> https://example.com/page2, https://example.com/page3", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void WriteGroupedOutput_UsesDeterministicSourceOrdering()
    {
        var writer = new OutputWriter();
        var originalOut = Console.Out;

        try
        {
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            writer.WriteEdge(new Uri("https://example.com/b"), new Uri("https://example.com/b1"));
            writer.WriteEdge(new Uri("https://example.com/a"), new Uri("https://example.com/a1"));
            writer.WriteGroupedOutput();

            var lines = consoleOutput
                .ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("https://example.com/a -> https://example.com/a1", lines[0]);
            Assert.Equal("https://example.com/b -> https://example.com/b1", lines[1]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task ExportAsync_GeneratesValidJson()
    {
        var writer = new OutputWriter();
        var source1 = new Uri("https://example.com/page1");
        var target1 = new Uri("https://example.com/page2");
        var target2 = new Uri("https://example.com/page3");
        var source2 = new Uri("https://example.com/page2");
        var target3 = new Uri("https://example.com/page4");

        // Suppress stdout
        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(source1, target1);
        writer.WriteEdge(source1, target2);
        writer.WriteEdge(source2, target3);

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "json");

            var json = await File.ReadAllTextAsync(tempFile);
            var data = JsonSerializer.Deserialize<List<JsonGroupedEdge>>(json);

            Assert.NotNull(data);
            Assert.Equal(2, data.Count);
            Assert.Equal("https://example.com/page1", data[0].SourceUrl);
            Assert.Equal(["https://example.com/page2", "https://example.com/page3"], data[0].Target);
            Assert.Equal("https://example.com/page2", data[1].SourceUrl);
            Assert.Equal(["https://example.com/page4"], data[1].Target);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_GeneratesValidHtml()
    {
        var writer = new OutputWriter();
        var source = new Uri("https://example.com/page1");
        var target1 = new Uri("https://example.com/page2");
        var target2 = new Uri("https://example.com/page3");

        // Suppress stdout
        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(source, target1);
        writer.WriteEdge(source, target2);

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "html");

            var html = await File.ReadAllTextAsync(tempFile);

            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<table>", html);
            Assert.Contains("<th>SourceUrl</th><th>Target</th>", html);
            Assert.Contains("rowspan=\"2\"", html);
            Assert.Contains("https://example.com/page1", html);
            Assert.Contains("https://example.com/page2", html);
            Assert.Contains("https://example.com/page3", html);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_GeneratesValidCsv()
    {
        var writer = new OutputWriter();
        var source = new Uri("https://example.com/page1");
        var target1 = new Uri("https://example.com/page2");
        var target2 = new Uri("https://example.com/page3");

        // Suppress stdout
        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(source, target1);
        writer.WriteEdge(source, target2);

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "csv");

            var csv = await File.ReadAllTextAsync(tempFile);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(3, lines.Length);
            Assert.Equal("SourceUrl,Target", lines[0].Trim());
            Assert.Contains("https://example.com/page1", lines[1]);
            Assert.Contains("https://example.com/page2", lines[1]);
            Assert.Contains("https://example.com/page1", lines[2]);
            Assert.Contains("https://example.com/page3", lines[2]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_Json_DeduplicatesAndOrdersTargets()
    {
        var writer = new OutputWriter();
        var source = new Uri("https://example.com/a");

        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(source, new Uri("https://example.com/z"));
        writer.WriteEdge(source, new Uri("https://example.com/a1"));
        writer.WriteEdge(source, new Uri("https://example.com/z")); // duplicate

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "json");

            var json = await File.ReadAllTextAsync(tempFile);
            var data = JsonSerializer.Deserialize<List<JsonGroupedEdge>>(json);

            Assert.NotNull(data);
            Assert.Single(data);
            Assert.Equal(["https://example.com/a1", "https://example.com/z"], data[0].Target);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_Json_OrdersSourcesDeterministically()
    {
        var writer = new OutputWriter();

        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(new Uri("https://example.com/b"), new Uri("https://example.com/b1"));
        writer.WriteEdge(new Uri("https://example.com/a"), new Uri("https://example.com/a1"));

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "json");

            var json = await File.ReadAllTextAsync(tempFile);
            var data = JsonSerializer.Deserialize<List<JsonGroupedEdge>>(json);

            Assert.NotNull(data);
            Assert.Equal(2, data.Count);
            Assert.Equal("https://example.com/a", data[0].SourceUrl);
            Assert.Equal("https://example.com/b", data[1].SourceUrl);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_Csv_GroupsMultipleTargetsUnderSource()
    {
        var writer = new OutputWriter();

        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(new Uri("https://example.com/b"), new Uri("https://example.com/b1"));
        writer.WriteEdge(new Uri("https://example.com/a"), new Uri("https://example.com/a1"));
        writer.WriteEdge(new Uri("https://example.com/a"), new Uri("https://example.com/a2"));

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "csv");

            var csv = await File.ReadAllTextAsync(tempFile);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // header + 3 data rows
            Assert.Equal(4, lines.Length);
            // a comes before b (deterministic ordering)
            Assert.Contains("example.com/a", lines[1]);
            Assert.Contains("example.com/a", lines[2]);
            Assert.Contains("example.com/b", lines[3]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_Html_UsesRowspanForGroupedSource()
    {
        var writer = new OutputWriter();

        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        writer.WriteEdge(new Uri("https://example.com/page1"), new Uri("https://example.com/a"));
        writer.WriteEdge(new Uri("https://example.com/page1"), new Uri("https://example.com/b"));
        writer.WriteEdge(new Uri("https://example.com/page1"), new Uri("https://example.com/c"));

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "html");

            var html = await File.ReadAllTextAsync(tempFile);

            // First row has rowspan="3" for the source cell
            Assert.Contains("rowspan=\"3\"", html);
            // All three targets are present
            Assert.Contains("https://example.com/a", html);
            Assert.Contains("https://example.com/b", html);
            Assert.Contains("https://example.com/c", html);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExportAsync_ThrowsOnUnsupportedFormat()
    {
        var writer = new OutputWriter();
        var tempFile = Path.GetTempFileName();

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await writer.ExportAsync(tempFile, "xml");
            });
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed record JsonGroupedEdge(string SourceUrl, List<string> Target);
}
