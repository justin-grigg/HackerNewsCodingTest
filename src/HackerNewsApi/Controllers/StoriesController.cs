using HackerNewsApi.Models;
using HackerNewsApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController(IHackerNewsService hackerNewsService) : ControllerBase
{
    private readonly IHackerNewsService hackerNewsService = hackerNewsService;

    [HttpGet("best")]
    [ProducesResponseType(typeof(IReadOnlyList<Story>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Story>>> GetBestStoriesAsync(
        [FromQuery] int n,
        CancellationToken cancellationToken)
    {
        var stories = await hackerNewsService.GetBestStoriesAsync(n, cancellationToken);

        return Ok(stories);
    }
}
