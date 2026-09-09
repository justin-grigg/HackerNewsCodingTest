# HackerNews API

A lightweight ASP.NET Core Web API that retrieves the best stories from the Hacker News Firebase API, enriches them with metadata (score, comment count, poster), and returns them sorted by score in descending order.

## Technologies

- **.NET 10** (minimal hosting model)
- **ASP.NET Core** Web API with Swagger documentation
- **HttpClient** with typed handler for external API calls
- **IMemoryCache** for in-process caching
- **SemaphoreSlim** for concurrent request throttling
- **xUnit** + **Moq** for testing

## Prerequisites

- .NET 10 SDK (or later)

## Running the application

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (development profile with Swagger UI)
dotnet run --project src/HackerNewsApi/HackerNewsApi.csproj
```

The API is available at `http://localhost:5195` by default. Swagger UI is available at `http://localhost:5195/swagger` in Development mode.

## API Usage

```bash
# Get the top N best stories sorted by score
curl "http://localhost:5195/api/stories/best?n=10"
```

**Query parameter:**
- `n` — number of stories to return (must be non-negative)

**Response:** Array of `Story` objects:

```json
[
  {
    "title": "Story Title",
    "uri": "https://example.com/article",
    "postedBy": "username",
    "time": "2024-05-29T14:53:20Z",
    "score": 150,
    "commentCount": 42
  }
]
```

## Configuration

Settings in `src/HackerNewsApi/appsettings.json`:

| Key | Default | Description |
|---|---|---|
| `HackerNewsService.BaseAddress` | `https://hacker-news.firebaseio.com/` | Base URL of the HackerNews Firebase API |
| `HackerNewsService.BestStoriesCacheSeconds` | `60` | Cache TTL for best story IDs |
| `HackerNewsService.ItemCacheSeconds` | `300` | Cache TTL for fetched items |
| `HackerNewsService.MissingItemCacheSeconds` | `60` | Cache TTL for items not found (404) |
| `HackerNewsService.MaxConcurrentItemRequests` | `16` | Max parallel requests for item fetch |

## Assumptions

- Stories with no `Url` field (text-only posts) fall back to `https://news.ycombinator.com/` rather than linking to the item's HackerNews discussion page.
- Missing/deleted items (HTTP 404) are cached with a short TTL and returned as `null`, then filtered out of results.
- The HackerNews API `beststories.json` endpoint returns story IDs in pre-sorted order; we fetch individual items and sort by score client-side.
- Timeouts for external HTTP calls are hard-coded to 10 seconds in `Program.cs`.

## Running tests

```bash
dotnet test tests/HackerNewsApi.Tests/HackerNewsApi.Tests.csproj
```

Tests use a mocked `HttpMessageHandler` so no external network calls are made.

## Enhancements for future

Given more time, I would:

- **Cache invalidation strategy** — Replace in-memory cache with Redis or a distributed cache so multiple instances share the same data and cache entries survive restarts.
- **Pagination / cursor support** — Allow clients to page through results rather than fetching the full top-N each time.
- **Resilience** — Add Polly or similar for retry, circuit breaker, and timeout policies on external HTTP calls.
- **Health checks** — Register a health check endpoint for the HackerNews API dependency.
- **Structured logging** — Switch to Serilog or OpenTelemetry for richer observability.

### Current Code Coverage Assessment

The existing test suite in `tests/HackerNewsApi.Tests/HackerNewsServiceTests.cs` provides **partial coverage** (approximately 60-70%) of the `HackerNewsService` codebase.

**Covered test scenarios:**
- `GetBestStoriesAsync` returns stories sorted by score in descending order
- `GetBestStoriesAsync` returns the requested count when fewer stories are available
- `GetBestStoriesAsync` returns an empty result when no stories are available
- `GetBestStoriesAsync` formats timestamps as ISO 8601
- `GetBestStoriesAsync` caches best story IDs on the first call
- `GetBestStoriesAsync` throws `ArgumentOutOfRangeException` for negative counts
- Mock HTTP handler responses for `beststories.json` and `v0/item/` endpoints

**Uncovered test scenarios (gaps):**
- **Item cache hit path** — No test verifies that `FetchItemAsync` returns directly from the in-memory cache when an item is already present
- **404 items mixed with successful items** — No test covers partial failures where some story IDs return 404 and others succeed
- **`HttpRequestException` handling** — No test for network failures during item fetches
- **`JsonException` handling** — No test for malformed JSON responses from the Hacker News API
- **`OperationCanceledException`** — No cancellation test
- **`ToDomainModel` null or whitespace title** — Returns `null` without a test
- **`ToDomainModel` null `Time`** — Falls back to the Unix epoch without a test
- **`ToDomainModel` null `Url`** — Falls back to `https://news.ycombinator.com/` without a test
- **`ToDomainModel` null `By`** — Falls back to `string.Empty` without a test
- **`topCount = 0` boundary case** — Only negative values are tested for throwing
- **`Dispose` method** — No disposal test
- **Double-check lock cache hit** — The second cache check after acquiring the lock is not explicitly tested

**Verdict:** The tests cover the main happy path and a few edge cases, but the error handling paths, cache-hit paths for individual items, and domain model edge cases remain untested. Key risks include untested error paths that could hide production failures, and the cache-hit path for individual items (a performance-critical path) that is completely unverified.
