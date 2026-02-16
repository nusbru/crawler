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
- **Domain-locked** — Automatically restricts crawling to the target domain only
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

## Design & Trade-offs

### Design Decisions

1. **Concurrency Model**:
   - **Decision**: Uses a **producer/consumer** pattern via `System.Threading.Channels` with a bounded capacity (1000 items).
   - **Why**: This provides natural backpressure (preventing memory exhaustion) and efficient worker coordination without complex locking or thread management. It scales well on a single node.
   - **Alternative**: `Task.WhenAll` with `SemaphoreSlim` was considered but rejected because it lacks the clean separation of queuing and processing that Channels provide.

2. **HTTP Strategy**:
   - **Decision**: A single `HttpClient` configured with `SocketsHttpHandler`.
   - **Why**: Enables connection pooling (avoiding socket exhaustion) and keeps the CLI runtime lightweight.
   - **Trade-off**: Sharing a single client means we lack granular timeouts or retry policies per-request, which could be improved with `IHttpClientFactory` and resilience policies (e.g., Polly) for production-grade robustness.

3. **HTML Parsing**:
   - **Decision**: Uses **AngleSharp** for HTML5-compliant parsing.
   - **Why**: It is robust, standards-aligned, and efficient for static HTML.
   - **Trade-off**: It does not execute JavaScript. Sites relying heavily on client-side rendering (SPAs) will not be fully crawled. This is a deliberate choice to avoid the heavy resource overhead of a headless browser (like Playwright/Selenium).

4. **Domain Locking**:
   - **Decision**: Strict host matching and URL normalization (stripping fragments).
   - **Why**: Prevents "leaking" the crawl to external sites and avoids duplicate work on `example.com/page#section`.

### Trade-offs & Future Improvements

- **Memory vs. Scale**: The crawler keeps visited URLs and results in memory (`ConcurrentDictionary`).
  - *Current*: Fast and simple for small-to-medium sites.
  - *Future*: For massive crawls, we would need an external store (Redis/SQLite) and streaming output to avoid OOM errors.

- **Error Handling**: Currently "fail-soft" (logs errors and continues).
  - *Current*: Ensures the crawl completes even if some pages fail.
  - *Future*: Add exponential backoff/retries for transient errors (429/503) to improve completeness.

- **Distribution**:
  - *Current*: Self-contained binaries (~60MB) for ease of use (no .NET runtime required).
  - *Trade-off*: Larger download size compared to framework-dependent builds.

## Development Environment

### Tools

- **IDE:** VS Code
- **Operating System:** Bluefin Linux
- **AI Tools:** GitHub Copilot

### AI Models Used

- **Claude Opus 4.6** (Planning)
- **OpenAI Codex** (Agent)
- **Gemini** (Documentation)

### VS Code Extensions

- **Adwaita Theme** (`piousdeer.adwaita-theme`)
- **Awesome Copilot** (`timheuer.awesome-copilot`)
- **Azure Repos** (`ms-vscode.azure-repos`)
- **C#** (`ms-dotnettools.csharp`)
- **C# Dev Kit** (`ms-dotnettools.csdevkit`)
- **Dev Containers** (`ms-vscode-remote.remote-containers`)
- **Docker (Containers)** (`ms-azuretools.vscode-containers`)
- **Entity Framework** (`richardwillis.vscode-entity-framework`)
- **Git Graph** (`mhutchie.git-graph`)
- **Git History** (`donjayamanne.githistory`)
- **GitHub Actions** (`github.vscode-github-actions`)
- **GitHub Copilot Chat** (`github.copilot-chat`)
- **GitHub Pull Requests** (`github.vscode-pull-request-github`)
- **GitHub Repositories (RemoteHub)** (`github.remotehub`)
- **Go** (`golang.go`)
- **Kubernetes Tools** (`ms-kubernetes-tools.vscode-kubernetes-tools`)
- **Markdown Mermaid** (`bierner.markdown-mermaid`)
- **markdownlint** (`davidanson.vscode-markdownlint`)
- **.NET Runtime** (`ms-dotnettools.vscode-dotnet-runtime`)
- **NuGet Gallery** (`patcx.vscode-nuget-gallery`)
- **Pylance** (`ms-python.vscode-pylance`)
- **Python** (`ms-python.python`)
- **Python Debugger** (`ms-python.debugpy`)
- **Python Environments** (`ms-python.vscode-python-envs`)
- **Rainbow Brackets** (`tal7aouy.rainbow-bracket`)
- **Rainbow CSV** (`mechatroner.rainbow-csv`)
- **Remote Explorer** (`ms-vscode.remote-explorer`)
- **Remote Repositories** (`ms-vscode.remote-repositories`)
- **Remote - SSH** (`ms-vscode-remote.remote-ssh`)
- **Remote - SSH: Editing** (`ms-vscode-remote.remote-ssh-edit`)
- **Terraform** (`hashicorp.terraform`)
- **vscode-icons** (`vscode-icons-team.vscode-icons`)
- **YAML** (`redhat.vscode-yaml`)

## Documentation

For detailed technical information, see the following documents:

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
