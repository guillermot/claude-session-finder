using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// The single writer of the session index.
/// </summary>
/// <remarks>
/// Implementations are expected to make each method one transaction, and only one caller writes
/// at a time: the store underneath permits exactly one writer, and batching a session's chunk
/// inserts into a single transaction is what keeps a full pass over the corpus in seconds.
/// </remarks>
public interface ISessionIndexWriter
{
    /// <summary>
    /// Reads back what the index holds for a session.
    /// </summary>
    /// <param name="sessionId">The session to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored state, or <see langword="null"/> when the session was never indexed.</returns>
    Task<IndexedSession?> GetIndexedSessionAsync(SessionId sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Upserts a session and replaces every chunk it owns.
    /// </summary>
    /// <param name="entry">The document, its origin and its watermark.</param>
    /// <param name="cancellationToken">Checked between chunk inserts, never mid-transaction commit.</param>
    /// <returns>A task that completes once the transaction is committed.</returns>
    Task WriteAsync(SessionIndexEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Adds what a pass over the appended tail of a transcript found, leaving the chunks written by
    /// earlier passes in place.
    /// </summary>
    /// <remarks>
    /// The chunks a pass derives rather than reads — the title, the folder, the resume prompt — are
    /// replaced, because a resumed pass produces them again from what it was told. Counters and
    /// timestamps on the row are merged rather than overwritten: a pass over the tail knows the
    /// last activity but not the first, and knows how many messages it read but not how many the
    /// session has.
    /// </remarks>
    /// <param name="entry">The tail document, its origin and its watermark.</param>
    /// <param name="cancellationToken">Checked between chunk inserts, never mid-transaction commit.</param>
    /// <returns>A task that completes once the transaction is committed.</returns>
    Task AppendAsync(SessionIndexEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a newly resolved title to an already indexed session, replacing its title chunk and
    /// leaving every other chunk untouched.
    /// </summary>
    /// <param name="revision">The new title and the watermark that found it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when a session row was updated.</returns>
    Task<bool> ReviseTitleAsync(SessionTitleRevision revision, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every indexed session that is not in the given set, together with its chunks.
    /// </summary>
    /// <param name="liveSessions">The sessions whose transcripts still exist on disk.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many sessions were removed.</returns>
    Task<int> PruneMissingAsync(IReadOnlyCollection<SessionId> liveSessions, CancellationToken cancellationToken);

    /// <summary>
    /// Compacts the index after a pass that rewrote sessions.
    /// </summary>
    /// <remarks>
    /// Replacing a session's chunks leaves the search index holding the removals as well as the
    /// replacements. Measured over the corpus, repeating a full pass without this grew the
    /// database by roughly half its own size each time.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the compaction.</param>
    /// <returns>A task that completes once the index is compacted.</returns>
    Task CompactAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rewrites the whole store so that space freed by deleted rows is returned to the file system.
    /// </summary>
    /// <remarks>
    /// This is not the same as <see cref="CompactAsync"/>, which merges the search index's own
    /// structures but leaves the file the size it grew to. Measured over the corpus, this reclaims
    /// roughly half the file after a pass that rewrote every session. It cannot run inside a
    /// transaction and it blocks every reader for its duration, so it belongs at the end of a pass
    /// the user asked for and nowhere else.
    /// </remarks>
    /// <param name="cancellationToken">Cancels before the rewrite starts.</param>
    /// <returns>A task that completes once the file has been rewritten.</returns>
    Task VacuumAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records that a reconcile pass completed, so the status slice can report how fresh the index is.
    /// </summary>
    /// <param name="completedAt">When the pass finished.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the value is stored.</returns>
    Task RecordReconcileAsync(DateTimeOffset completedAt, CancellationToken cancellationToken);
}
