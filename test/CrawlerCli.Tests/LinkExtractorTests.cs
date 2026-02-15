namespace CrawlerCli.Tests;

public class LinkExtractorTests
{
    [Fact]
    public async Task ExtractLinksAsync_ExtractsAnchorHrefs()
    {
        var extractor = new LinkExtractor();
        var html = """
            <html>
            <body>
                <a href="/page1">Link 1</a>
                <a href="/page2">Link 2</a>
                <a href="https://example.com/page3">Link 3</a>
            </body>
            </html>
            """;

        var links = await extractor.ExtractLinksAsync(html);

        Assert.Equal(3, links.Count);
        Assert.Contains("/page1", links);
        Assert.Contains("/page2", links);
        Assert.Contains("https://example.com/page3", links);
    }

    [Fact]
    public async Task ExtractLinksAsync_IgnoresAnchorsWithoutHref()
    {
        var extractor = new LinkExtractor();
        var html = """
            <html>
            <body>
                <a>No href</a>
                <a href="">Empty href</a>
                <a href="/valid">Valid</a>
            </body>
            </html>
            """;

        var links = await extractor.ExtractLinksAsync(html);

        Assert.Single(links);
        Assert.Contains("/valid", links);
    }

    [Fact]
    public async Task ExtractLinksAsync_HandlesMultipleLinksToSameTarget()
    {
        var extractor = new LinkExtractor();
        var html = """
            <html>
            <body>
                <a href="/page1">Link 1</a>
                <a href="/page1">Link 1 again</a>
                <a href="/page2">Link 2</a>
            </body>
            </html>
            """;

        var links = await extractor.ExtractLinksAsync(html);

        Assert.Equal(3, links.Count);
        Assert.Equal(2, links.Count(l => l == "/page1"));
    }

    [Fact]
    public async Task ExtractLinksAsync_ExtractsRelativeUrls()
    {
        var extractor = new LinkExtractor();
        var html = """
            <html>
            <body>
                <a href="../parent">Parent</a>
                <a href="./sibling">Sibling</a>
                <a href="child">Child</a>
            </body>
            </html>
            """;

        var links = await extractor.ExtractLinksAsync(html);

        Assert.Equal(3, links.Count);
        Assert.Contains("../parent", links);
        Assert.Contains("./sibling", links);
        Assert.Contains("child", links);
    }

    [Fact]
    public async Task ExtractLinksAsync_HandlesEmptyPage()
    {
        var extractor = new LinkExtractor();
        var html = "<html><body></body></html>";

        var links = await extractor.ExtractLinksAsync(html);

        Assert.Empty(links);
    }
}