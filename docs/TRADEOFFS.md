# Architecture Trade-offs: High-Performance Concurrent Web Crawler

## Purpose
This document complements [ADR-0001](ADR.md) with a decision-by-decision trade-off analysis of the current crawler implementation.

## Scope
- Current implementation in `src/CrawlerCli`
- Operational model and distribution choices from CI/CD workflows
- Forward-looking alternatives and when to adopt them

---

## Decision 1: Bounded `Channel<Uri>` + Worker Pool Concurrency

### Current Choice
- Producer-consumer frontier via `Channel<Uri>` with capacity `1000`
- `BoundedChannelFullMode.Wait` for backpressure
- Configurable worker pool (`--max-parallel`, default `5`)

### Positives
- Natural flow control prevents unbounded queue growth.
- Good throughput for I/O-heavy crawling workloads.
- Clear lifecycle signaling via channel completion.

### Negatives
- Fixed capacity may underutilize resources on very large, healthy targets.
- Coordination uses explicit lock/counters, adding complexity.
- Single-process concurrency model does not scale beyond one node.

### Alternatives
- `SemaphoreSlim` + `Task.WhenAll` for simpler bounded parallel fetch.
- TPL Dataflow for richer pipeline primitives and backpressure policies.
- Distributed queue (Redis/Kafka/SQS) for multi-node crawling.

### Recommendation / Adoption Trigger
- Keep current model for single-node crawls up to medium scale.
- Revisit when queue pressure is persistent (frequent writer blocking) or when horizontal scale is required.

---

## Decision 2: Single `HttpClient` with `SocketsHttpHandler`

### Current Choice
- Single shared `HttpClient` instance per crawler run
- `SocketsHttpHandler` with connection pooling
- Redirect cap (`5`) and request timeout (`30s`)

### Positives
- Avoids socket exhaustion through connection reuse.
- Low overhead and predictable behavior for CLI runtime.
- Straightforward operational model without extra dependencies.

### Negatives
- No built-in retry/backoff for transient failures and `429`.
- One timeout policy for all targets and page sizes.
- Shared client settings are coarse-grained under highly mixed workloads.

### Alternatives
- `IHttpClientFactory` + named clients for policy separation.
- Add resilience policies (retry with jitter, rate limiting, circuit breaker).
- Per-host concurrency/rate controls using .NET rate limiter primitives.

### Recommendation / Adoption Trigger
- Keep current setup for fast, low-dependency CLI usage.
- Add resilience/rate-limit policies when crawl reliability under noisy networks or restrictive hosts becomes a priority.

---

## Decision 3: Domain Locking + URL Normalization + Concurrent Visited Set

### Current Choice
- Scope restricted to base host and subdomains.
- URL normalization removes fragments and normalizes trailing slash behavior.
- Deduplication via `ConcurrentDictionary<Uri, byte>`.

### Positives
- Strong safety against external-domain crawl leaks.
- Thread-safe deduplication with low contention.
- Reduces duplicate work from trivial URL variants.

### Negatives
- Canonicalization is intentionally limited (query normalization is not applied).
- Host-based scope can be too broad or too narrow for some enterprise domain models.
- Internationalized/edge URL normalization cases are not deeply specialized.

### Alternatives
- Policy-driven scope rules (allow/deny host/path patterns).
- Canonicalization profiles (preserve/remove selected query keys).
- Public Suffix List-aware eTLD+1 scoping for stricter domain semantics.

### Recommendation / Adoption Trigger
- Keep current behavior as safe default for generic domain crawls.
- Introduce policy-driven scope/canonicalization when users need tenant-level precision or analytics-grade URL identity.

---

## Decision 4: AngleSharp Parsing (Anchor `href` Extraction)

### Current Choice
- Parse HTML with AngleSharp and collect `a[href]`.
- Resolve relative links against the current page URL.

### Positives
- Standards-aligned parsing with good correctness on malformed HTML.
- Lightweight compared to browser automation approaches.
- Sufficient for static link-graph discovery.

### Negatives
- Misses JavaScript-rendered links and runtime navigation events.
- Ignores other discovery vectors (sitemaps, meta refresh, script-generated routes).
- Parsing all pages fully can become CPU-heavy at high throughput.

### Alternatives
- HtmlAgilityPack for a simpler non-HTML5 parser option.
- Hybrid mode: static crawl first, selective browser rendering for missed pages.
- Pluggable extractors (HTML + sitemap + custom signals).

### Recommendation / Adoption Trigger
- Keep AngleSharp-first as default for performance and portability.
- Add hybrid rendering only when critical target content is primarily JS-generated.

---

## Decision 5: Fail-Soft Error Handling (Log and Continue)

### Current Choice
- Worker-level exceptions are logged to STDERR.
- Crawl continues after request/parsing errors.
- No retry policy for transient classes by default.

### Positives
- High crawl continuity; one bad page does not stop progress.
- Simple failure semantics for CLI users.
- Lower implementation complexity.

### Negatives
- Potentially lower completeness due to one-shot request failures.
- No explicit distinction between transient and permanent failures.
- Limited operational observability beyond console logs.

### Alternatives
- Status-aware retries with bounded exponential backoff and jitter.
- Structured metrics/report output (failure classes, retries, skipped reasons).
- Optional "strict mode" that fails fast on policy-critical errors.

### Recommendation / Adoption Trigger
- Keep fail-soft default for usability and speed.
- Add retries/telemetry when completeness and run-to-run reproducibility become contractual requirements.

---

## Decision 6: In-Memory Edge Collection + End-of-Run Export

### Current Choice
- Crawl edges are accumulated in-memory in `OutputWriter`.
- Final output is grouped and emitted to stdout or exported (`json`, `html`, `csv`).

### Positives
- Simple implementation with deterministic grouped output.
- Easy support for multiple export formats from one in-memory model.
- Minimal moving parts for CLI users.

### Negatives
- Memory usage grows with total edge count.
- No incremental/streaming export for very large crawls.
- Run interruption can lose non-exported in-memory results.

### Alternatives
- Streaming writers (append-only JSONL/CSV) during crawl.
- Embedded store (SQLite/LiteDB) for large result sets and resumability.
- Periodic checkpoint export for long-running jobs.

### Recommendation / Adoption Trigger
- Keep current model for short-to-medium crawls.
- Move to streaming/checkpointed output when memory pressure or long-run durability requirements appear.

---

## Decision 7: Distribution Strategy (Self-Contained Native + Flatpak + Docker)

### Current Choice
- Native self-contained single-file artifacts for Windows/macOS.
- Linux distribution through Flatpak.
- Docker image distribution for container workflows.

### Positives
- Broad install paths across desktop and server environments.
- Good user experience (minimal runtime prerequisites).
- Reproducible release packaging through CI/CD automation.

### Negatives
- Larger artifact sizes for self-contained binaries.
- Higher CI/CD complexity across packaging channels.
- Platform matrix maintenance cost grows over time.

### Alternatives
- Framework-dependent binaries (smaller artifacts, runtime pre-req).
- Expand native Linux tarball strategy to align with desktop/server conventions.
- Multi-arch Docker images for broader deployment coverage.

### Recommendation / Adoption Trigger
- Keep multi-channel distribution as long as user adoption is split.
- Simplify channels if maintenance cost outweighs user value, or expand Linux-native artifacts if direct binary demand grows.

---

## Suggested Near-Term Priorities
1. Add bounded retry/backoff for transient HTTP failures (`408`, `429`, `5xx`).
2. Add optional rate-limit controls (global and per-host).
3. Add optional streaming export mode for large crawls.
4. Add structured crawl summary (visited, failed, skipped, duration).

These improvements preserve the current architecture while reducing operational risk at higher scale.
