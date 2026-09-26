using System.Collections.Concurrent;

namespace SessionFinder.Infrastructure.Indexing;

/// <summary>
/// Collects reported changes by path and hands each one over when it is worth acting on.
/// </summary>
/// <remarks>
/// <para>
/// Keying by path is what makes a live session cheap. A transcript being written continuously
/// produces a notification every few hundred milliseconds, and every one of them collapses onto the
/// same entry, so the indexer sees one unit of work rather than hundreds.
/// </para>
/// <para>
/// Readiness has two edges, and the second is not a refinement of the first: waiting for the writing
/// to stop is exactly wrong for the file that matters most. The session being worked in right now is
/// written continuously for as long as it is being worked in, so a trailing edge alone would hold it
/// back forever and the live session would be the one thing the index never had. The first edge
/// keeps a finished file from being read while it is still being written; the second guarantees that
/// an unfinished one is read anyway.
/// </para>
/// <para>
/// Time is taken from an injected <see cref="TimeProvider"/> so both edges can be proven without
/// waiting for either of them.
/// </para>
/// </remarks>
public sealed class PendingChangeBuffer(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, PendingChange> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many distinct paths are waiting.</summary>
    public int Count => _pending.Count;

    /// <summary>
    /// Records that a path was reported as changed, folding it onto any entry already there.
    /// </summary>
    /// <param name="filePath">Absolute path of the file that changed.</param>
    public void Record(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var now = timeProvider.GetUtcNow();

        _pending.AddOrUpdate(
            filePath,
            _ => new PendingChange(now, now),
            (_, existing) => existing with { LastSeenUtc = now });
    }

    /// <summary>
    /// Removes and returns the paths that are ready to be indexed.
    /// </summary>
    /// <param name="settleFor">How long a path must go unreported before it is considered finished.</param>
    /// <param name="waitAtMost">How long a path may be held back while it keeps being reported.</param>
    /// <returns>The paths to index now, in no particular order.</returns>
    public IReadOnlyList<string> TakeReady(TimeSpan settleFor, TimeSpan waitAtMost)
    {
        var now = timeProvider.GetUtcNow();
        var ready = new List<string>();

        foreach (var entry in _pending)
        {
            if (!IsReady(entry.Value, now, settleFor, waitAtMost))
            {
                continue;
            }

            if (_pending.TryRemove(entry))
            {
                ready.Add(entry.Key);
            }
        }

        return ready;
    }

    /// <summary>
    /// Removes and returns every waiting path, whether or not it has settled. For shutdown, where
    /// the choice is between indexing what is known now and forgetting it.
    /// </summary>
    /// <returns>Every path that was waiting.</returns>
    public IReadOnlyList<string> TakeAll()
    {
        var all = new List<string>(_pending.Count);

        foreach (var entry in _pending)
        {
            if (_pending.TryRemove(entry))
            {
                all.Add(entry.Key);
            }
        }

        return all;
    }

    /// <summary>
    /// A path is ready once it has gone quiet, or once it has been waiting long enough that quiet
    /// is not coming.
    /// </summary>
    private static bool IsReady(PendingChange change, DateTimeOffset now, TimeSpan settleFor, TimeSpan waitAtMost) =>
        now - change.LastSeenUtc > settleFor || now - change.FirstSeenUtc > waitAtMost;
}
