using HackerNewsApi.Models;

namespace HackerNewsApi.Services;

public interface IHackerNewsService
{
    Task<IReadOnlyList<Story>> GetBestStoriesAsync(int topCount, CancellationToken cancellationToken);
}