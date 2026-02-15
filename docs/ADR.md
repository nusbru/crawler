---
title: "ADR-0001: High-Performance Concurrent Web Crawler Architecture"
status: "Accepted"
date: "2026-02-15"
authors: "bruno"
tags: ["architecture", "decision", "concurrency", "performance"]
supersedes: ""
superseded_by: ""
---

# ADR-0001: High-Performance Concurrent Web Crawler Architecture

## Status

**Accepted**

## Context

The project requires a high-performance, domain-locked web crawler implemented as a CLI application in C# (.NET 10). The application must efficiently traverse websites by following internal links while maintaining strict domain boundaries. Key requirements include:

- **Resource Efficiency**: Maintain low memory footprint without browser engines (no Playwright/Selenium)
- **Concurrency**: Maximize I/O throughput through asynchronous operations and parallel processing
- **Domain Isolation**: Prevent external domain "leaks" while crawling internal links
- **Robustness**: Handle HTTP errors, timeouts, non-HTML content, and infinite redirects gracefully
- **Deployment**: Multi-platform distribution (Windows, macOS, Linux) with zero external dependencies

The solution must avoid heavyweight web scraping frameworks (e.g., Scrapy) and headless browsers to minimize resource consumption and maximize performance.

## Decision

We have adopted a **Producer-Consumer architecture** using `System.Threading.Channels` as the foundational concurrency pattern, combined with lightweight HTTP-based crawling and strategic architectural components:

### Core Architecture Components

**CMP-001**: **Producer-Consumer Pattern with Bounded Channels**
- Use `Channel<Uri>` (bounded capacity: 1,000 items) as the URL frontier queue
- Worker pool with configurable parallelism (default: 5 workers, CLI configurable via `--max-parallel`)
- `BoundedChannelFullMode.Wait` for automatic backpressure to prevent memory exhaustion

**CMP-002**: **HttpClient with SocketsHttpHandler**
- Singleton `HttpClient` instance with pooled connection management
- `SocketsHttpHandler` configuration:
  - `PooledConnectionLifetime`: 5 minutes
  - `PooledConnectionIdleTimeout`: 2 minutes
  - `MaxAutomaticRedirections`: 5
  - Request timeout: 30 seconds

**CMP-003**: **AngleSharp HTML Parser**
- Standards-compliant HTML5 parsing library
- Lightweight alternative to HtmlAgilityPack
- Used exclusively for extracting `<a href>` links from HTML documents

**CMP-004**: **Domain-Locking with URL Normalization**
- `ConcurrentDictionary<Uri, byte>` for thread-safe visited URL tracking
- Host-based domain validation (base domain extracted from initial URL)
- URL normalization strategy:
  - Strip URI fragments (`#section`)
  - Normalize trailing slashes
  - Convert relative paths to absolute URLs

**CMP-005**: **Self-Contained Native Deployment**
- Single-file executables for Windows (x64), macOS (x64/ARM64), Linux (x64)
- `PublishSingleFile=true` with .NET runtime embedded
- `PublishTrimmed=false` to preserve full runtime compatibility
- Multi-format export support (JSON, HTML, CSV)

## Consequences

### Positive

- **POS-001**: **High Throughput** — Channels provide efficient producer-consumer coordination with minimal lock contention, enabling high-concurrency crawling without thread synchronization overhead
- **POS-002**: **Memory Safety** — Bounded channel (1,000-item capacity) prevents unbounded memory growth during crawls of large sites
- **POS-003**: **Connection Pool Efficiency** — SocketsHttpHandler prevents socket exhaustion through connection reuse, critical for high-volume HTTP requests
- **POS-004**: **Zero External Dependencies** — Self-contained deployment eliminates runtime installation requirements, simplifying distribution across platforms
- **POS-005**: **Predictable Resource Usage** — Pure HTTP-based crawling (no browser engine) provides consistent memory and CPU profiles regardless of page complexity
- **POS-006**: **Domain Isolation Guarantee** — Host-based validation with ConcurrentDictionary ensures thread-safe deduplication and prevents external domain crawling
- **POS-007**: **Standards Compliance** — AngleSharp provides HTML5-compliant parsing, reducing edge case failures on modern web pages
- **POS-008**: **Automatic Flow Control** — BoundedChannelFullMode.Wait provides natural backpressure, slowing producers when consumers are overwhelmed

### Negative

- **NEG-001**: **JavaScript Limitation** — HTTP-only crawling cannot extract links from JavaScript-rendered content (SPAs, dynamic content loaded via AJAX)
- **NEG-002**: **Fixed Channel Capacity** — 1,000-item bound may be suboptimal for extremely large sites (10,000+ pages), potentially creating backpressure bottlenecks
- **NEG-003**: **No Rate Limiting** — Current implementation lacks built-in rate limiting or exponential backoff for 429 responses, potentially triggering rate limits on restrictive servers
- **NEG-004**: **Single HttpClient Instance** — Shared client across all workers may create contention under extreme parallelism (50+ workers)
- **NEG-005**: **Binary Size Overhead** — Self-contained deployment includes full .NET runtime (~60-80 MB per platform), increasing distribution size compared to framework-dependent builds
- **NEG-006**: **No Distributed Crawling** — Architecture does not support distributed crawling across multiple machines for planet-scale crawls
- **NEG-007**: **Limited Error Recovery** — Workers log errors to STDERR but do not implement retry logic for transient failures (network timeouts, 5xx errors)

## Alternatives Considered

### HtmlAgilityPack for HTML Parsing

- **ALT-001**: **Description**: Use HtmlAgilityPack, an alternative HTML parsing library with broader .NET ecosystem adoption
- **ALT-002**: **Rejection Reason**: HtmlAgilityPack uses lenient HTML parsing (not HTML5-compliant), leading to edge case failures on modern web standards. AngleSharp provides standards-compliant parsing with similar performance characteristics and more predictable behavior.

### Task.WhenAll with SemaphoreSlim for Concurrency

- **ALT-002**: **Description**: Use `SemaphoreSlim` to limit parallelism with `Task.WhenAll` instead of Channels
- **ALT-003**: **Rejection Reason**: Producer-consumer pattern with Channels provides cleaner separation of concerns (queue management vs. work processing). Channels offer built-in backpressure and completion signaling, while SemaphoreSlim requires manual coordination logic and lacks queue semantics.

### Framework-Dependent Deployment

- **ALT-004**: **Description**: Distribute as framework-dependent binaries requiring users to install .NET 10 runtime separately
- **ALT-005**: **Rejection Reason**: Adds friction to end-user experience (runtime installation prerequisite). Self-contained deployment aligns with CLI tool expectations (single executable, no dependencies). Binary size overhead (~60-80 MB) is acceptable trade-off for user convenience.

### Unbounded Channel

- **ALT-006**: **Description**: Use unbounded channel (`Channel.CreateUnbounded<Uri>()`) to eliminate backpressure blocking
- **ALT-007**: **Rejection Reason**: Unbounded channels risk unbounded memory growth when crawling sites with millions of links. Bounded channel with backpressure provides natural flow control, ensuring memory usage remains predictable even on pathological inputs (infinite link graphs).

## Implementation Notes

- **IMP-001**: **Worker Coordination** — Workers use lock-based coordination (`_coordinationLock`) to track active worker count and queued items. When `_activeWorkers == 0 && _queuedItems == 0`, the channel writer is completed to signal termination.
- **IMP-002**: **Content-Type Validation** — Workers check `Content-Type: text/html` in response headers before downloading response body, avoiding unnecessary bandwidth consumption for binary files (PDFs, images, videos).
- **IMP-003**: **Graceful Shutdown** — `Console.CancelKeyPress` handler creates linked cancellation token to propagate Ctrl+C interrupts to all worker tasks, ensuring clean shutdown.
- **IMP-004**: **URL Normalization** — `UrlNormalizer.Normalize()` method strips fragments and trailing slashes before inserting URLs into visited dictionary, preventing duplicate crawls of equivalent URLs.
- **IMP-005**: **Multi-Platform Build Matrix** — GitHub Actions CD workflow builds native binaries for `win-x64`, `osx-x64`, `osx-arm64`, and `linux-x64` runtime identifiers, packaging as `.exe` (Windows) and `.tar.gz` (macOS/Linux).
- **IMP-006**: **Export Formats** — `OutputWriter` class supports JSON (structured), HTML (visual sitemap), and CSV (tabular) formats, selectable via `--format` and `--output-path` CLI options.
- **IMP-007**: **Test Coverage** — Unit tests cover `LinkExtractor`, `UrlNormalizer`, `OutputWriter`, and integration tests validate end-to-end crawl behavior (`WebCrawlerBehaviorTests`).

## References

- **REF-001**: [docs/PRD.md](PRD.md) — Product Requirements Document defining performance targets and functional requirements
- **REF-002**: [docs/FRD.md](FRD.md) — Functional Requirements Document specifying technical stack and system architecture
- **REF-003**: [docs/CICD.md](CICD.md) — CI/CD Pipeline Strategy documenting multi-platform deployment architecture
- **REF-004**: [System.Threading.Channels Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — Microsoft documentation on producer-consumer patterns with Channels
- **REF-005**: [SocketsHttpHandler Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines) — HttpClient connection pooling guidance
- **REF-006**: [AngleSharp GitHub Repository](https://github.com/AngleSharp/AngleSharp) — HTML5 parsing library documentation
