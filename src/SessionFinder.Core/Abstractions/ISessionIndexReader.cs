using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Reads the session index without holding the writer's connection.
/// </summary>
public interface ISessionIndexReader
{
    /// <summary>
    /// Measures the index.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The snapshot, or an empty one when no usable database is present: an index that has not
    /// been built yet is a normal state, not a failure.
    /// </returns>
    Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds the sessions whose text matches the request.
    /// </summary>
    /// <param name="request">What to look for, how many to return, and how to weigh a match.</param>
    /// <param name="cancellationToken">Checked between result rows.</param>
    /// <returns>
    /// The candidates in lexical order, or the most recently active sessions when the request
    /// carries nothing searchable. An index that has not been built yet yields no candidates
    /// rather than an error: it is a normal state on a first run.
    /// </returns>
    Task<IReadOnlyList<SessionMatch>> SearchAsync(SessionSearchRequest request, CancellationToken cancellationToken);
}
