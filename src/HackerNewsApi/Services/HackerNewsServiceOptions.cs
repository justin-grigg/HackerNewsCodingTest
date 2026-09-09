namespace HackerNewsApi.Services;

internal sealed class HackerNewsServiceOptions
{
    public int BestStoriesCacheSeconds { get; set; } = 60;

    public int ItemCacheSeconds { get; set; } = 300;

    public int MissingItemCacheSeconds { get; set; } = 60;

    public int MaxConcurrentItemRequests { get; set; } = 16;
}
