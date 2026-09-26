namespace SessionFinder.Core.Features.IndexStatus;

/// <summary>
/// Reports what is in the index.
/// </summary>
public interface IGetIndexStatusHandler
{
    /// <summary>
    /// Measures the index.
    /// </summary>
    /// <param name="query">The query, which carries no arguments.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    Task<IndexStatusResult> HandleAsync(GetIndexStatusQuery query, CancellationToken cancellationToken);
}
