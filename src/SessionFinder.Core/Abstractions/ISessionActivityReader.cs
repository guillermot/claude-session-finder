namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Reads what was said in which session over a window of time, which is the raw material of the
/// daily recap.
/// </summary>
public interface ISessionActivityReader
{
    /// <summary>
    /// Finds every session with a message inside the window, together with those messages.
    /// </summary>
    /// <param name="from">Start of the window, inclusive.</param>
    /// <param name="to">End of the window, exclusive.</param>
    /// <param name="includeAssistantText">
    /// Whether assistant turns are returned as well as human prompts. They are large, and only the
    /// day being summarised needs them.
    /// </param>
    /// <param name="cancellationToken">Checked between rows.</param>
    /// <returns>
    /// The sessions with activity, or none when the index has not been built yet.
    /// </returns>
    Task<IReadOnlyList<SessionActivity>> GetActivityAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        bool includeAssistantText,
        CancellationToken cancellationToken);
}
