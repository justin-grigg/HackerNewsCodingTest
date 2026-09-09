using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNewsApi.Models;
using HackerNewsApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace HackerNewsApi.Tests;

public class HackerNewsServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsStoriesSortedByScoreDescendingAsync()
    {
        var ids = new long[] { 1, 2 };
        var item1 = new HackerNewsItem
        {
            Id = 1,
            By = "user1",
            Descendants = 10,
            Score = 100,
            Time = 0,
            Title = "Low Score",
            Url = "https://example.com/1",
        };
        var item2 = new HackerNewsItem
        {
            Id = 2,
            By = "user2",
            Descendants = 20,
            Score = 200,
            Time = 0,
            Title = "High Score",
            Url = "https://example.com/2",
        };

        var httpHandler = new MockHttpMessageHandler(ids, new Dictionary<long, HackerNewsItem>
        {
            [1] = item1,
            [2] = item2,
        });

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://example.com/"),
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewsService>>();
        var options = new HackerNewsServiceOptions();

        var sut = new HackerNewsService(httpClient, cache, logger, options);

        var result = await sut.GetBestStoriesAsync(2, CancellationToken.None);

        var stories = result.ToList();
        Assert.Equal(2, stories.Count);
        Assert.Equal("High Score", stories[0].Title);
        Assert.Equal(200, stories[0].Score);
        Assert.Equal("Low Score", stories[1].Title);
        Assert.Equal(100, stories[1].Score);
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsRequestedCountAsync()
    {
        var ids = new long[] { 1, 2, 3 };
        var item1 = MakeItem(1, "Story 1", 100);
        var item2 = MakeItem(2, "Story 2", 90);
        var item3 = MakeItem(3, "Story 3", 80);

        var httpHandler = new MockHttpMessageHandler(ids, new Dictionary<long, HackerNewsItem>
        {
            [1] = item1,
            [2] = item2,
            [3] = item3,
        });

        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://example.com/"),
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewsService>>();
        var options = new HackerNewsServiceOptions();

        var sut = new HackerNewsService(httpClient, cache, logger, options);

        var result = await sut.GetBestStoriesAsync(2, CancellationToken.None);

        var stories = result.ToList();
        Assert.Equal(2, stories.Count);
        Assert.Contains(stories, s => s.Title == "Story 1");
        Assert.Contains(stories, s => s.Title == "Story 2");
        Assert.DoesNotContain(stories, s => s.Title == "Story 3");
    }

    [Fact]
    public async Task GetBestStoriesAsync_ReturnsEmptyWhenNoStoriesAvailableAsync()
    {
        var httpHandler = new MockHttpMessageHandler(Array.Empty<long>(), new Dictionary<long, HackerNewsItem>());
        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://example.com/"),
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewsService>>();
        var options = new HackerNewsServiceOptions();

        var sut = new HackerNewsService(httpClient, cache, logger, options);

        var result = await sut.GetBestStoriesAsync(5, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBestStoriesAsync_TimeIsFormattedAsIso8601Async()
    {
        var ids = new long[] { 1 };
        var unixTime = 1570887781;
        var item = new HackerNewsItem
        {
            Id = 1,
            By = "ismaildonmez",
            Descendants = 588,
            Score = 1757,
            Time = unixTime,
            Title = "A uBlock Origin update was rejected from the Chrome Web Store",
            Url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
        };

        var httpHandler = new MockHttpMessageHandler(ids, new Dictionary<long, HackerNewsItem> { [1] = item });
        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://example.com/"),
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewsService>>();
        var options = new HackerNewsServiceOptions();

        var sut = new HackerNewsService(httpClient, cache, logger, options);

        var result = await sut.GetBestStoriesAsync(1, CancellationToken.None);

        var story = result.Single();
        Assert.Equal("2019-10-12T13:43:01+00:00", story.Time);
    }

    [Fact]
    public async Task GetBestStoriesAsync_CachesBestStoryIdsOnFirstCallAsync()
    {
        var ids = new long[] { 1 };
        var item = MakeItem(1, "Story 1", 100);

        var httpHandler = new MockHttpMessageHandler(ids, new Dictionary<long, HackerNewsItem> { [1] = item });
        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://example.com/"),
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewsService>>();
        var options = new HackerNewsServiceOptions { BestStoriesCacheSeconds = 3600 };

        var sut = new HackerNewsService(httpClient, cache, logger, options);

        await sut.GetBestStoriesAsync(1, CancellationToken.None).ConfigureAwait(false);
        await sut.GetBestStoriesAsync(1, CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(1, httpHandler.BestStoriesCallCount);
    }

    [Theory]
    [InlineData(-1)]
    public async Task GetBestStoriesAsync_ThrowsForInvalidCountAsync(int count)
    {
        var httpHandler = new MockHttpMessageHandler(new long[] { 1 }, new Dictionary<long, HackerNewsItem>());
        var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri("https://example.com/"),
        };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<HackerNewsService>>();
        var options = new HackerNewsServiceOptions();

        var sut = new HackerNewsService(httpClient, cache, logger, options);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.GetBestStoriesAsync(count, CancellationToken.None));
    }

    private static HackerNewsItem MakeItem(int id, string title, int score) =>
        new()
        {
            Id = id,
            By = $"user{id}",
            Descendants = id * 10,
            Score = score,
            Time = 0,
            Title = title,
            Url = $"https://example.com/{id}",
        };

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly long[] _bestStoryIds;
        private readonly IReadOnlyDictionary<long, HackerNewsItem> _items;

        public int BestStoriesCallCount { get; private set; }

        public MockHttpMessageHandler(long[] bestStoryIds, IReadOnlyDictionary<long, HackerNewsItem> items)
        {
            _bestStoryIds = bestStoryIds;
            _items = items;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (path.Contains("beststories.json"))
            {
                BestStoriesCallCount++;
                var content = JsonContent.Create(_bestStoryIds, options: JsonOptions);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            if (path.StartsWith("/v0/item/") || path.StartsWith("v0/item/"))
            {
                var id = path.Split('/').Last().Replace(".json", "");
                if (long.TryParse(id, out var parsed) && _items.TryGetValue(parsed, out var item))
                {
                    var content = JsonContent.Create(item, options: JsonOptions);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}