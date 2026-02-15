# Product Requirements Document: High-Performance CLI Web Crawler

## 1. Executive Summary
The goal is to build a high-performance, domain-locked web crawler in C#. The application will accept a base URL, traverse all internal links, and output the relationship between pages (Source URL -> Found URLs).

## 2. Goals & Objectives
- **Efficiency:** Maximize I/O throughput using asynchronous programming.
- **Accuracy:** Correctly identify and normalize internal links while ignoring external domains.
- **Resource Management:** Maintain a low memory footprint by avoiding browser engines (headless browsers).

## 3. Functional Requirements (Summary)
- Input: Single valid URL.
- Process: Recursive crawl of the same domain/sub-domain only.
- Output: STDOUT stream showing the Parent URL followed by its discovered children.
- Constraint: No Scrapy-like frameworks or Playwright/Selenium.

## 5. Non-Functional Requirements
- **Performance:** Use `HttpClient` connection pooling to prevent socket exhaustion.
- **Concurrency:** Implement a Producer/Consumer pattern to process multiple URLs in parallel.
- **Robustness:** Graceful handling of timeouts, 4xx, and 5xx HTTP errors.

## 6. Success Metrics
- Successfully crawls a 1,000-page site in under 60 seconds (network permitting).
- Zero "leaks" to external domains (e.g., clicking a Twitter link and crawling Twitter).