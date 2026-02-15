<div align="center">

# Crawler

[![Build Status](https://img.shields.io/github/actions/workflow/status/nusbru/crawler/build.yml?style=flat-square&label=Build)](https://github.com/nusbru/crawler/actions)
![.NET](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square&logo=dotnet&logoColor=white)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

A high-performance, domain-locked CLI web crawler built with C# and .NET 10.

[Features](#features) • [Installation](#installation) • [Usage](#usage) • [Export Formats](#export-formats) • [Docker](#docker) • [Documentation](#documentation)

</div>

## Features

- **Concurrent crawling** — Producer/consumer architecture using `System.Threading.Channels` for maximum throughput
- **Domain-locked** — Automatically restricts crawling to the target domain and its subdomains
- **URL normalization** — Deduplicates URLs by stripping fragments and normalizing trailing slashes
- **Multiple export formats** — Output results as JSON, HTML, or CSV
- **Resilient** — Graceful handling of timeouts, HTTP errors, non-HTML content, and infinite redirects
- **Lightweight** — Pure HTTP-based crawling with no browser engine overhead

## Installation

### Pre-built Binaries

Download the latest release for your platform from the [Releases page](https://github.com/nusbru/crawler/releases):

- **Windows** — `crawler-win-x64.zip` (native executable)
- **macOS** — `crawler-osx-x64.tar.gz` (native executable)
- **Linux** — `crawler-linux-x64.tar.gz` (native executable)
- **Linux (Flatpak)** — `io.github.bruno.crawler.flatpak` (sandboxed package)

### Building from Source

**Prerequisites:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

**Build:**

```bash
dotnet build
```

**Run tests:**

```bash
dotnet test
```

## Usage

```bash
dotnet run --project src/CrawlerCli -- --url https://example.com
```

### Options

| Option | Description | Default |
| :--- | :--- | :--- |
| `--url` | The starting URL to crawl *(required)* | — |
| `--max-parallel` | Maximum number of concurrent workers | `5` |
| `--format` | Export format (`json`, `html`, `csv`) | — |
| `--outputPath` | Path to write the export file | — |

### Examples

**Basic crawl with default settings:**

```bash
dotnet run --project src/CrawlerCli -- --url https://example.com
```

**Crawl with 10 concurrent workers:**

```bash
dotnet run --project src/CrawlerCli -- --url https://example.com --max-parallel 10
```

**Export results to JSON:**

```bash
dotnet run --project src/CrawlerCli -- --url https://example.com --format json --outputPath results.json
```

### Default output

When no export format is specified, results are printed to stdout grouped by source URL:

```
https://example.com -> https://example.com/about, https://example.com/contact
https://example.com/about -> https://example.com, https://example.com/team
```

## Export Formats

| Format | Description |
| :--- | :--- |
| `json` | Structured JSON with source URLs and their discovered targets |
| `html` | Styled HTML table for easy viewing in a browser |
| `csv` | Comma-separated values with `SourceUrl,Target` columns |

> [!NOTE]
> Both `--format` and `--outputPath` must be specified together for export to work.

## Docker

Build and run the crawler as a container:

```bash
docker build -t crawler .
docker run --rm -v "$PWD:/work" crawler --url https://www.example.com --format json --outputPath /work/results.json

```

## Architecture

The crawler uses a **producer/consumer** pattern with these core components:

| Component | Responsibility |
| :--- | :--- |
| `WebCrawler` | Orchestrates the crawl using a bounded `Channel<Uri>` frontier and concurrent workers |
| `LinkExtractor` | Parses HTML with [AngleSharp](https://anglesharp.github.io/) to extract anchor hrefs |
| `UrlNormalizer` | Resolves relative URLs, strips fragments, and enforces domain scope |
| `OutputWriter` | Collects crawl edges and handles grouped console output or file export |

The `HttpClient` is configured with `SocketsHttpHandler` for connection pooling and reuse, preventing socket exhaustion during large crawls.

## Documentation

For detailed technical information about the project, see the following documents:

- **[PRD.md](docs/PRD.md)** — Product Requirements Document outlining goals, objectives, and constraints
- **[FRD.md](docs/FRD.md)** — Functional Requirements Document detailing the technical stack and system architecture
- **[ADR.md](docs/ADR.md)** — Architecture Decision Records documenting key design choices and rationale
- **[TRADEOFFS.md](docs/TRADEOFFS.md)** — Trade-off analysis of architecture choices, alternatives, and adoption triggers
- **[CICD.md](docs/CICD.md)** — CI/CD Pipeline Strategy covering build automation and deployment workflows

## Flatpak

A [Flatpak](https://flatpak.org/) manifest is provided for Linux desktop distribution:

```bash
# Requires a pre-built self-contained binary in artifacts/publish/linux-x64/
flatpak-builder build packaging/flatpak/io.github.bruno.crawler.json
```
