namespace CrawlerCli;

/// <summary>
/// Provides URL normalization and validation functionality.
/// </summary>
internal static class UrlNormalizer
{
    /// <summary>
    /// Normalizes a URL by removing fragments and ensuring consistent trailing slash handling.
    /// </summary>
    public static Uri Normalize(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        // Ensure consistent trailing slash for paths
        var path = builder.Path;
        if (path.Length > 1 && path.EndsWith('/'))
        {
            builder.Path = path.TrimEnd('/');
        }
        else if (path.Length == 0 || path == "/")
        {
            builder.Path = "/";
        }

        return builder.Uri;
    }

    /// <summary>
    /// Resolves a potentially relative URL against a base URL.
    /// </summary>
    public static Uri? ResolveUrl(Uri baseUri, string href)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        href = href.Trim();

        // Skip non-HTTP schemes
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("#", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return new Uri(baseUri, href);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a URL is within the allowed domain scope.
    /// </summary>
    public static bool IsInScope(Uri targetUri, Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(targetUri);
        ArgumentNullException.ThrowIfNull(baseUri);

        // Must be HTTP or HTTPS
        if (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var targetHost = targetUri.Host.ToLowerInvariant();
        var baseHost = baseUri.Host.ToLowerInvariant();

        // Exact match or subdomain
        return targetHost == baseHost || targetHost.EndsWith($".{baseHost}");
    }
}
