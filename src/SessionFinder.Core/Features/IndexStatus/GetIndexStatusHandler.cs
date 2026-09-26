using SessionFinder.Core.Abstractions;

namespace SessionFinder.Core.Features.IndexStatus;

/// <summary>
/// Answers "is the index there, and is it any good": counts, damaged sessions, unrecovered
/// folders, size on disk and how long ago it was last brought up to date.
/// </summary>
/// <remarks>
/// The slice is deliberately thin. It exists because it is how every indexing milestone is
/// verified without a user interface, not because the measurement itself needs a use case.
/// </remarks>
public sealed class GetIndexStatusHandler(ISessionIndexReader reader) : IGetIndexStatusHandler
{
    /// <inheritdoc />
    public async Task<IndexStatusResult> HandleAsync(GetIndexStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var statistics = await reader.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);

        return new IndexStatusResult(statistics);
    }
}
