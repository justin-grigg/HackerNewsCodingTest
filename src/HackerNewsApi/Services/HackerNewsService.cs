using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackerNewsApi.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HackerNewsApi.Services;

internal sealed class HackerNewsService : IHackerNewsService, IDisposable
{
    internal const string BestStoriesCacheKey = "hn:beststories";
    internal const string ItemCacheKeyPrefix = "hn:item:";
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;
    private readonly IMemoryCache hackerNewsItemCache;
    private readonly ILogger<HackerNewsService> logger;
    private readonly HackerNewsServiceOptions hackerNewsServiceOptions;
    private readonly SemaphoreSlim bestStoriesLock = new(1, 1);
    private bool isDisposed;

    public HackerNewsService(HttpClient httpClient, IMemoryCache hackerNewsItemCache, ILogger<HackerNewsService> logger, HackerNewsServiceOptions options)
    {
        this.httpClient = httpClient;
        this.hackerNewsItemCache = hackerNewsItemCache;
        this.logger = logger;
        hackerNewsServiceOptions = options;
    }

    private static readonly Action<ILogger, long, Exception?> LogFailedToFetchItem =
        LoggerMessage.Define<long>(
            LogLevel.Warning,
            new EventId(1, nameof(LogFailedToFetchItem)),
            "Failed to fetch Hacker News item {ItemId}");

    private static readonly Action<ILogger, Exception?> LogFetchingBestStoryIds =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(2, nameof(LogFetchingBestStoryIds)),
            "Fetching best story ids from Hacker News API");

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        bestStoriesLock.Dispose();
        isDisposed = true;
    }

    public async Task<IReadOnlyList<Story>> GetBestStoriesAsync(int topCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(topCount);

        var ids = await GetBestStoryIdsAsync(cancellationToken);

        if (ids.Length == 0)
        {
            return [];
        }
        
        var topCountOfBestStoryIds = ids.Take(topCount).ToArray();
        
        var topNewsItems = await FetchNewsItemsAsync(topCountOfBestStoryIds, cancellationToken);
        
        var topNews = topNewsItems.Where(newsItem => newsItem is not null).Select(MapToDomainModel);

        var topStories = topNews
        .Where(story => story is not null)
        .Select(story => story!)
        .OrderByDescending(story => story.Score)
        .ToList();

        return topStories;
    }

    private async Task<long[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        if (hackerNewsItemCache.TryGetValue<long[]>(BestStoriesCacheKey, out var cachedBestStoriesKey) && cachedBestStoriesKey is not null)
        {
            return cachedBestStoriesKey;
        }

        await bestStoriesLock.WaitAsync(cancellationToken);
        
        try
        {
            if (hackerNewsItemCache.TryGetValue(BestStoriesCacheKey, out cachedBestStoriesKey) && cachedBestStoriesKey is not null)
            {
                return cachedBestStoriesKey;
            }
            
            LogFetchingBestStoryIds(logger, null);

            var ids = await httpClient.GetFromJsonAsync<long[]>("v0/beststories.json", JsonOptions, cancellationToken) ?? [];
            
            hackerNewsItemCache.Set(BestStoriesCacheKey, ids, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(hackerNewsServiceOptions.BestStoriesCacheSeconds) });
            return ids;
        }
        finally
        {
            bestStoriesLock.Release();
        }
    }

    private async Task<IReadOnlyList<HackerNewsItem?>> FetchNewsItemsAsync(long[] ids, CancellationToken cancellationToken)
    {
        var hackerNewsResults = new HackerNewsItem?[ids.Length];
        
        using var serviceRequestGate = new SemaphoreSlim(hackerNewsServiceOptions.MaxConcurrentItemRequests);
        
        var requestTasks = new List<Task>(ids.Length);
        
        for (var i = 0; i < ids.Length; i++)
        {
            var index = i;
            var id = ids[index];

            requestTasks.Add(Task.Run(async () =>
            {
                await serviceRequestGate.WaitAsync(cancellationToken);
                
                try
                {
                    hackerNewsResults[index] = await FetchItemAsync(id, cancellationToken);
                }
                finally
                {
                    serviceRequestGate.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(requestTasks);
        return hackerNewsResults;
    }

    private async Task<HackerNewsItem?> FetchItemAsync(long id, CancellationToken cancellationToken)
    {
        var key = ItemCacheKeyPrefix + id;

        if (hackerNewsItemCache.TryGetValue<HackerNewsItem>(key, out var cachedHackerNewsItem) && cachedHackerNewsItem is not null)
        {
            return cachedHackerNewsItem;
        }
        
        try
        {
            var itemUri = new Uri(httpClient.BaseAddress!, $"v0/item/{id}.json");
            var response = await httpClient.GetAsync(itemUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                hackerNewsItemCache.Set(
                    key,
                    new HackerNewsItem { Id = id },
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(hackerNewsServiceOptions.MissingItemCacheSeconds),
                    });
                return null;
            }

            response.EnsureSuccessStatusCode();
            var item = await response.Content.ReadFromJsonAsync<HackerNewsItem>(JsonOptions, cancellationToken);
            
            if (item is null)
            {
                return null;
            }

            hackerNewsItemCache.Set(key, item, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(hackerNewsServiceOptions.ItemCacheSeconds) });
            return item;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            LogFailedToFetchItem(logger, id, ex);
            return null;
        }
        catch (JsonException ex)
        {
            LogFailedToFetchItem(logger, id, ex);
            return null;
        }
    }

    private static Story? MapToDomainModel(HackerNewsItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Title))
        {
            return null;
        }

        const string siteFallbackForEmptyStoryUri = "https://news.ycombinator.com/";
        var unixEpochDateZero = DateTimeOffset.FromUnixTimeSeconds(0).ToUniversalTime();
        var postedAtDate = item.Time is { } t ? unixEpochDateZero.AddSeconds(t) : unixEpochDateZero;

        return new Story
        {
            Title = item.Title,
            Uri = item.Url is not null ? new Uri(item.Url) : new Uri(siteFallbackForEmptyStoryUri),
            PostedBy = item.By ?? string.Empty,
            Time = postedAtDate.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture),
            Score = item.Score ?? 0,
            CommentCount = item.Descendants ?? 0,
        };
    }
}