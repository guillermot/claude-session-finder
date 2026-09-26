namespace SessionFinder.Core.Features.Search;

/// <summary>
/// Answers what the user typed with a ranked list of sessions.
/// </summary>
public interface ISearchSessionsHandler
{
    /// <summary>
    /// Runs one search.
    /// </summary>
    /// <param name="query">What the user typed and how many results to return.</param>
    /// <param name="cancellationToken">Cancels the lookup; a keystroke supersedes the one before it.</param>
    /// <returns>The ranked results, or the most recent sessions when there is nothing to search for.</returns>
    Task<SearchSessionsResult> HandleAsync(SearchSessionsQuery query, CancellationToken cancellationToken);
}
