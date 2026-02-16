namespace CrawlerCli.Tests;

public class UrlNormalizerTests
{
    [Fact]
    public void Normalize_RemovesFragment()
    {
        var uri = new Uri("https://example.com/page#section");
        var normalized = UrlNormalizer.Normalize(uri);

        Assert.Equal("https://example.com/page", normalized.ToString());
    }

    [Fact]
    public void Normalize_RemovesTrailingSlashFromPath()
    {
        var uri = new Uri("https://example.com/page/");
        var normalized = UrlNormalizer.Normalize(uri);

        Assert.Equal("https://example.com/page", normalized.ToString());
    }

    [Fact]
    public void Normalize_PreservesRootPath()
    {
        var uri = new Uri("https://example.com/");
        var normalized = UrlNormalizer.Normalize(uri);

        Assert.Equal("https://example.com/", normalized.ToString());
    }

    [Fact]
    public void Normalize_HandlesMultipleNormalizations()
    {
        var uri1 = new Uri("https://example.com/page#intro");
        var uri2 = new Uri("https://example.com/page/");
        var uri3 = new Uri("https://example.com/page");

        var normalized1 = UrlNormalizer.Normalize(uri1);
        var normalized2 = UrlNormalizer.Normalize(uri2);
        var normalized3 = UrlNormalizer.Normalize(uri3);

        Assert.Equal(normalized1, normalized2);
        Assert.Equal(normalized2, normalized3);
    }

    [Fact]
    public void ResolveUrl_ConvertsRelativeToAbsolute()
    {
        var baseUri = new Uri("https://example.com/path/page.html");
        var href = "../other/file.html";

        var resolved = UrlNormalizer.ResolveUrl(baseUri, href);

        Assert.NotNull(resolved);
        Assert.Equal("https://example.com/other/file.html", resolved.ToString());
    }

    [Fact]
    public void ResolveUrl_HandlesAbsoluteUrls()
    {
        var baseUri = new Uri("https://example.com/page");
        var href = "https://other.com/page";

        var resolved = UrlNormalizer.ResolveUrl(baseUri, href);

        Assert.NotNull(resolved);
        Assert.Equal("https://other.com/page", resolved.ToString());
    }

    [Fact]
    public void ResolveUrl_SkipsMailtoLinks()
    {
        var baseUri = new Uri("https://example.com/page");
        var href = "mailto:test@example.com";

        var resolved = UrlNormalizer.ResolveUrl(baseUri, href);

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveUrl_SkipsJavascriptLinks()
    {
        var baseUri = new Uri("https://example.com/page");
        var href = "javascript:void(0)";

        var resolved = UrlNormalizer.ResolveUrl(baseUri, href);

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveUrl_SkipsFragmentOnlyLinks()
    {
        var baseUri = new Uri("https://example.com/page");
        var href = "#section";

        var resolved = UrlNormalizer.ResolveUrl(baseUri, href);

        Assert.Null(resolved);
    }

    [Fact]
    public void IsInScope_AcceptsExactHostMatch()
    {
        var baseUri = new Uri("https://example.com/");
        var targetUri = new Uri("https://example.com/page");

        var inScope = UrlNormalizer.IsInScope(targetUri, baseUri);

        Assert.True(inScope);
    }

    [Fact]
    public void IsInScope_AcceptsSubdomain()
    {
        var baseUri = new Uri("https://example.com/");
        var targetUri = new Uri("https://www.example.com/page");

        var inScope = UrlNormalizer.IsInScope(targetUri, baseUri);

        Assert.False(inScope);
    }

    [Fact]
    public void IsInScope_RejectsExternalDomain()
    {
        var baseUri = new Uri("https://example.com/");
        var targetUri = new Uri("https://other.com/page");

        var inScope = UrlNormalizer.IsInScope(targetUri, baseUri);

        Assert.False(inScope);
    }

    [Fact]
    public void IsInScope_RejectsSuperDomain()
    {
        var baseUri = new Uri("https://www.example.com/");
        var targetUri = new Uri("https://example.com/page");

        var inScope = UrlNormalizer.IsInScope(targetUri, baseUri);

        Assert.False(inScope);
    }

    [Fact]
    public void IsInScope_RejectsNonHttpSchemes()
    {
        var baseUri = new Uri("https://example.com/");
        var targetUri = new Uri("ftp://example.com/file");

        var inScope = UrlNormalizer.IsInScope(targetUri, baseUri);

        Assert.False(inScope);
    }

    [Fact]
    public void IsInScope_HandlesCaseInsensitivity()
    {
        var baseUri = new Uri("https://Example.Com/");
        var targetUri = new Uri("https://example.com/page");

        var inScope = UrlNormalizer.IsInScope(targetUri, baseUri);

        Assert.True(inScope);
    }
}
