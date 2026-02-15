using AngleSharp.Html.Parser;

namespace CrawlerCli;

/// <summary>
/// Extracts links from HTML content.
/// </summary>
internal sealed class LinkExtractor
{
    private readonly HtmlParser _parser = new();

    /// <summary>
    /// Extracts all href values from anchor tags in the HTML content.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExtractLinksAsync(string htmlContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlContent);

        var document = await _parser.ParseDocumentAsync(htmlContent, cancellationToken);
        var anchors = document.QuerySelectorAll("a[href]");

        var links = new List<string>();
        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                links.Add(href);
            }
        }

        return links;
    }
}
