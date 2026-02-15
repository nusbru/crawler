using System.CommandLine;
using CrawlerCli;

var rootCommand = new RootCommand("A concurrent web crawler that discovers and maps links within a domain");

var urlOption = new Option<string>(
    name: "--url")
{
    Description = "The starting URL to crawl",
    Required = true
};

var maxParallelOption = new Option<int?>(
    name: "--max-parallel")
{
    Description = "Maximum number of concurrent workers"
};

var formatOption = new Option<string?>(
    name: "--format")
{
    Description = "Output format for export (json, html, csv)"
};

var outputPathOption = new Option<string?>(
    name: "--outputPath")
{
    Description = "Path to write the export file"
};

rootCommand.Options.Add(urlOption);
rootCommand.Options.Add(maxParallelOption);
rootCommand.Options.Add(formatOption);
rootCommand.Options.Add(outputPathOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var url = parseResult.GetValue(urlOption);
    var maxParallel = parseResult.GetValue(maxParallelOption);
    var format = parseResult.GetValue(formatOption);
    var outputPath = parseResult.GetValue(outputPathOption);

    // Validate URL
    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var startUri))
    {
        await Console.Error.WriteLineAsync($"Error: Invalid URL '{url}'");
        return 1;
    }

    if (startUri.Scheme != Uri.UriSchemeHttp && startUri.Scheme != Uri.UriSchemeHttps)
    {
        await Console.Error.WriteLineAsync("Error: URL must use HTTP or HTTPS scheme");
        return 1;
    }

    // Validate max-parallel
    var effectiveMaxParallel = maxParallel ?? 5;
    if (effectiveMaxParallel <= 0)
    {
        await Console.Error.WriteLineAsync("Error: --max-parallel must be greater than 0");
        return 1;
    }

    // Validate format and outputPath
    var supportedFormats = new[] { "json", "html", "csv" };
    if (!string.IsNullOrWhiteSpace(format))
    {
        if (!supportedFormats.Contains(format.ToLowerInvariant()))
        {
            await Console.Error.WriteLineAsync($"Error: Unsupported format '{format}'. Supported formats: {string.Join(", ", supportedFormats)}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            await Console.Error.WriteLineAsync("Error: --outputPath is required when --format is specified");
            return 1;
        }
    }
    else if (!string.IsNullOrWhiteSpace(outputPath))
    {
        await Console.Error.WriteLineAsync("Error: --format is required when --outputPath is specified");
        return 1;
    }

    // Run the crawler
    var outputWriter = new OutputWriter();
    using var crawler = new WebCrawler(startUri, effectiveMaxParallel, outputWriter);

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    ConsoleCancelEventHandler cancelHandler = (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        await crawler.CrawlAsync(cts.Token);

        // Export if requested
        if (!string.IsNullOrWhiteSpace(format) && !string.IsNullOrWhiteSpace(outputPath))
        {
            await outputWriter.ExportAsync(outputPath, format, cts.Token);
            await Console.Error.WriteLineAsync($"Results exported to {outputPath}");
        }
        else
        {
            outputWriter.WriteGroupedOutput();
        }

        return 0;
    }
    catch (OperationCanceledException)
    {
        await Console.Error.WriteLineAsync("Crawl cancelled by user");
        return 130;
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync($"Fatal error: {ex.Message}");
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
});

return await rootCommand.Parse(args).InvokeAsync();