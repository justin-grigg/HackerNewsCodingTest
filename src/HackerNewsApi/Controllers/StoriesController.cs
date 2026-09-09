using HackerNewsApi.Models;
using HackerNewsApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController : ControllerBase
{
    private readonly IHackerNewsService _hackerNewsService;

    public StoriesController(IHackerNewsService hackerNewsService)
    {
        _hackerNewsService = hackerNewsService;
    }

    [HttpGet("best")]
    [ProducesResponseType(typeof(IReadOnlyList<Story>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Story>>> GetBestStoriesAsync(
        [FromQuery] int n,
        CancellationToken cancellationToken)
    {
        var stories = await _hackerNewsService.GetBestStoriesAsync(n, cancellationToken);

        return Ok(stories);
    }
}
