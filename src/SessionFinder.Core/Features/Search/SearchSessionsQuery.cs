namespace SessionFinder.Core.Features.Search;

/// <summary>
/// Asks for the sessions that best answer what the user typed.
/// </summary>
public sealed record SearchSessionsQuery
{
    /// <summary>
    /// Whatever is in the search box, including nothing at all. Input too short to search on is
    /// answered with the most recently active sessions rather than with an error.
    /// </summary>
    public required string? Text { get; init; }

    /// <summary>
    /// How many results to return. Left unset, the configured maximum applies.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// Builds a query for a piece of typed text.
    /// </summary>
    /// <param name="text">Whatever is in the search box.</param>
    /// <returns>The query, with the configured result count.</returns>
    public static SearchSessionsQuery For(string? text) => new() { Text = text };
}
