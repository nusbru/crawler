using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CrawlerCli;

/// <summary>
/// Concurrent web crawler with domain-locking and deduplication.
/// </summary>
internal sealed class WebCrawler : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly LinkExtractor _linkExtractor;
    private readonly OutputWriter _outputWriter;
    private readonly Uri _baseUri;
    private readonly int _maxParallel;
    private readonly ConcurrentDictionary<Uri, byte> _visited = new();
    private readonly Channel<Uri> _frontier;
    private int _activeWorkers = 0;
    private int _queuedItems = 0;
    private readonly object _coordinationLock = new();

    public WebCrawler(Uri startUri, int maxParallel, OutputWriter outputWriter)
    {
        ArgumentNullException.ThrowIfNull(startUri);
        ArgumentNullException.ThrowIfNull(outputWriter);

        if (maxParallel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallel), "Max parallel must be greater than 0");
        }

        _baseUri = startUri;
        _maxParallel = maxParallel;
        _outputWriter = outputWriter;
        _linkExtractor = new LinkExtractor();

        // Configure HttpClient with pooled handler
        var handler = new SocketsHttpHandler
        {
            MaxAutomaticRedirections = 5,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            AllowAutoRedirect = true
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WebCrawlerCli/1.0");

        // Create bounded channel for backpressure
        _frontier = Channel.CreateBounded<Uri>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Starts the crawl operation.
    /// </summary>
    public async Task CrawlAsync(CancellationToken cancellationToken = default)
    {
        // Enqueue the starting URL
        var normalizedStart = UrlNormalizer.Normalize(_baseUri);
        await EnqueueUrlAsync(normalizedStart, cancellationToken);

        // Start worker tasks
        var workers = new List<Task>();
        for (int i = 0; i < _maxParallel; i++)
        {
            workers.Add(WorkerAsync(cancellationToken));
        }

        // Wait for all workers to complete
        await Task.WhenAll(workers);
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Try to read from the channel
                if (!await _frontier.Reader.WaitToReadAsync(cancellationToken))
                {
                    // Channel is completed
                    break;
                }

                if (_frontier.Reader.TryRead(out var url))
                {
                    lock (_coordinationLock)
                    {
                        _activeWorkers++;
                        _queuedItems--;
                    }

                    try
                    {
                        await ProcessUrlAsync(url, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await Console.Error.WriteLineAsync($"Error processing {url}: {ex.Message}");
                    }
                    finally
                    {
                        lock (_coordinationLock)
                        {
                            _activeWorkers--;
                            // If no workers are active and queue is empty, complete the channel
                            if (_activeWorkers == 0 && _queuedItems == 0)
                            {
                                _frontier.Writer.TryComplete();
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ChannelClosedException)
            {
                break;
            }
        }
    }

    private async Task ProcessUrlAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            // Fetch the page
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Check content type
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                await Console.Error.WriteLineAsync($"Skipping non-HTML content: {url} (Content-Type: {contentType})");
                return;
            }

            // Handle HTTP errors
            if (!response.IsSuccessStatusCode)
            {
                await Console.Error.WriteLineAsync($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {url}");
                return;
            }

            // Read and parse HTML
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var links = await _linkExtractor.ExtractLinksAsync(html, cancellationToken);

            // Process discovered links
            foreach (var href in links)
            {
                var resolvedUri = UrlNormalizer.ResolveUrl(url, href);
                if (resolvedUri == null)
                {
                    continue;
                }

                var normalizedUri = UrlNormalizer.Normalize(resolvedUri);

                // Check scope
                if (!UrlNormalizer.IsInScope(normalizedUri, _baseUri))
                {
                    continue;
                }

                // Output the edge
                _outputWriter.WriteEdge(url, normalizedUri);

                // Enqueue for crawling
                await EnqueueUrlAsync(normalizedUri, cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"Request failed for {url}: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            await Console.Error.WriteLineAsync($"Timeout fetching {url}");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, rethrow
            throw;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Unexpected error processing {url}: {ex.Message}");
        }
    }

    private async Task EnqueueUrlAsync(Uri url, CancellationToken cancellationToken)
    {
        // Deduplicate using visited set
        if (_visited.TryAdd(url, 0))
        {
            lock (_coordinationLock)
            {
                _queuedItems++;
            }

            try
            {
                await _frontier.Writer.WriteAsync(url, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                // Channel closed, stop enqueueing
                lock (_coordinationLock)
                {
                    _queuedItems--;
                }
            }
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
