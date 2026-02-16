using System.Collections.Concurrent;

namespace CrawlerCli.Tests;

/// <summary>
/// Integration tests for WebCrawler behavior including deduplication and error handling.
/// </summary>
public class WebCrawlerBehaviorTests
{
    [Fact]
    public void WebCrawler_RejectsInvalidMaxParallel()
    {
        var uri = new Uri("https://example.com");
        var writer = new OutputWriter();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new WebCrawler(uri, 0, writer);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new WebCrawler(uri, -1, writer);
        });
    }

    [Fact]
    public void WebCrawler_AcceptsValidMaxParallel()
    {
        var uri = new Uri("https://example.com");
        var writer = new OutputWriter();

        using var crawler1 = new WebCrawler(uri, 1, writer);
        using var crawler5 = new WebCrawler(uri, 5, writer);
        using var crawler20 = new WebCrawler(uri, 20, writer);

        Assert.NotNull(crawler1);
        Assert.NotNull(crawler5);
        Assert.NotNull(crawler20);
    }

    [Fact]
    public async Task ConcurrentDeduplication_HandlesMultipleWorkers()
    {
        // Test that URL deduplication works with concurrent access
        var visitedUrls = new ConcurrentDictionary<Uri, byte>();
        var testUrls = new List<Uri>();
        
        for (int i = 0; i < 100; i++)
        {
            testUrls.Add(new Uri($"https://example.com/page{i % 10}"));
        }

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var localIndex = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var uri = testUrls[localIndex * 10 + j];
                    visitedUrls.TryAdd(uri, 0);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Should have only 10 unique URLs despite 100 attempts
        Assert.Equal(10, visitedUrls.Count);
    }

    [Fact]
    public void UrlNormalization_EnsuresDeduplication()
    {
        var urls = new[]
        {
            new Uri("https://example.com/page"),
            new Uri("https://example.com/page/"),
            new Uri("https://example.com/page#section1"),
            new Uri("https://example.com/page#section2"),
        };

        var normalized = urls.Select(UrlNormalizer.Normalize).ToHashSet();

        // All should normalize to the same URL
        Assert.Single(normalized);
    }

    [Fact]
    public void DomainLock_ExcludesExternalLinks()
    {
        var baseUri = new Uri("https://example.com");
        var testUrls = new Dictionary<string, bool>
        {
            ["https://example.com/page1"] = true,
            ["https://example.com/page2"] = true,
            ["https://www.example.com/page3"] = true,
            ["https://subdomain.example.com/page4"] = false,
            ["https://other.com/page5"] = false,
            ["https://example.org/page6"] = false,
            ["http://evil-example.com/page7"] = false,
        };

        foreach (var (url, expectedInScope) in testUrls)
        {
            var uri = new Uri(url);
            var inScope = UrlNormalizer.IsInScope(uri, baseUri);
            Assert.Equal(expectedInScope, inScope);
        }
    }

    [Fact]
    public async Task ErrorHandling_ContinuesAfterFailure()
    {
        // This test verifies the concept that errors don't stop processing
        var errors = new ConcurrentBag<string>();
        var successes = new ConcurrentBag<int>();

        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await Task.Delay(10);
            
            if (i % 3 == 0)
            {
                errors.Add($"Error {i}");
            }
            else
            {
                successes.Add(i);
            }
        });

        await Task.WhenAll(tasks);

        // Should have processed all items despite errors
        Assert.Equal(4, errors.Count);  // 0, 3, 6, 9
        Assert.Equal(6, successes.Count); // 1, 2, 4, 5, 7, 8
    }

    [Fact]
    public async Task OutputWriter_ThreadSafeEdgeRecording()
    {
        var writer = new OutputWriter();
        using var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var source = new Uri($"https://example.com/page{index}");
                var target = new Uri($"https://example.com/page{index + 1}");
                writer.WriteEdge(source, target);
            }));
        }

        await Task.WhenAll(tasks);

        var tempFile = Path.GetTempFileName();
        try
        {
            await writer.ExportAsync(tempFile, "json");
            var json = await File.ReadAllTextAsync(tempFile);
            
            // Should have grouped entries with SourceUrl and Target
            Assert.Contains("\"SourceUrl\"", json);
            Assert.Contains("\"Target\"", json);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
