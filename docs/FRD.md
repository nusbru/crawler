# Functional Requirements Document: High-Performance CLI Web Crawler

## 1. Technical Stack
- **Language:** C# (.NET 10)
- **Networking:** `System.Net.Http.HttpClient` (Singleton or IHttpClientFactory).
- **Parsing:** `HtmlAgilityPack` or `AngleSharp`.
- **Concurrency:** `System.Threading.Channels` for the URL frontier.
- **CLI:** `System.CommandLine` for argument parsing.

## 2. System Architecture
The application must follow a **Producer-Consumer** architecture to ensure thread safety and maximum throughput.



### 2.1 The Frontier (Data Structure)
- Use a `Channel<Uri>` to manage the crawl queue.
- Use a `ConcurrentDictionary<Uri, byte>` to track visited URLs and prevent cycles.

### 2.2 Domain Scope Logic
- **Base Domain:** Extracted from the initial input URL (e.g., `example.com`).
- **Validation:** Only URLs where `Uri.Host` matches the base domain or is a sub-path of it are added to the Frontier.
- **Normalization:** Strip fragments (`#`) and trailing slashes to ensure `site.com/page` and `site.com/page/` are not crawled twice.

## 3. Detailed Logic Flow
1. **Initialization:**
   - Initialize `HttpClient` with a `SocketsHttpHandler` to optimize connection reuse.
   - Accept `startUrl` from CLI.
2. **The Worker Loop (Multiple Tasks):**
   - **Step A:** Dequeue URL from `Channel`.
   - **Step B:** Perform `GET` request. Verify `Content-Type` is `text/html`.
   - **Step C:** Parse HTML for all `<a>` tags with an `href`.
   - **Step D:** Convert relative paths to absolute URLs using the current page as a base.
   - **Step E:** If URL is internal AND not visited, add to `Channel` and `Visited` set.
   - **Step F:** Export to JSON or HTML or CSV (--format and --outputPath) if is setup from CLI if not print in console following the format`[Source URL] -> [Discovered URL]`.
3. **Termination:**
   - The program exits when the `Channel` is empty and all active worker tasks are idle.

## 4. Error & Edge Case Handling
| Scenario | Action |
| :--- | :--- |
| Non-HTML File (PDF/Image) | Check Header; Abort download before reading body. |
| 404 Not Found | Log error to STDERR; Continue crawling other links. |
| Rate Limiting (429) | Implement basic exponential backoff or limit `MaxDegreeOfParallelism`. |
| Infinite Redirects | Set `HttpClient.MaxAutomaticRedirections`. |

## 5. Execution Interface
The tool should be invoked as follows:
`dotnet run --url https://example.com --max-parallel 10`