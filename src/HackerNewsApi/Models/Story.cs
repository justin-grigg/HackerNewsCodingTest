namespace HackerNewsApi.Models;

/// <summary>
/// Represents a Hacker News story returned by the API.
/// </summary>
public sealed class Story
{
    /// <summary>
    /// The title of the story.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The URL of the story, or null if it is a self-post.
    /// </summary>
    public required Uri? Uri { get; init; }

    /// <summary>
    /// The username of the poster.
    /// </summary>
    public required string PostedBy { get; init; }

    /// <summary>
    /// The time the story was posted, in ISO 8601 format with UTC timezone.
    /// </summary>
    public required string Time { get; init; }

    /// <summary>
    /// The score of the story.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>
    /// The number of comments on the story.
    /// </summary>
    public required int CommentCount { get; init; }
}
