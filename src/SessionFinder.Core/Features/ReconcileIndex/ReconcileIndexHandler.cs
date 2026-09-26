using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexSessionFile;

namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// Enumerates the transcripts on disk, indexes the ones that need it, and removes the sessions
/// whose transcripts are gone.
/// </summary>
/// <remarks>
/// <para>
/// What to do with one transcript belongs to <see cref="IIndexSessionFileHandler"/>, which the
/// watcher drives too: a reconcile pass is the same work done over every file at once, and letting
/// the two diverge would mean a session indexed by the watcher and a session indexed by a pass
/// could end up meaning different things.
/// </para>
/// <para>
/// What is left here is what only a whole pass can do: the order files are visited in, the tally,
/// and the removal of sessions whose transcript has gone, which cannot be decided from any single
/// file.
/// </para>
/// </remarks>
public sealed class ReconcileIndexHandler(
    ISessionFileCatalog catalog,
    IIndexSessionFileHandler indexer,
    ISessionIndexWriter writer,
    TimeProvider timeProvider,
    ILogger<ReconcileIndexHandler> logger) : IReconcileIndexHandler
{
    /// <inheritdoc />
    public async Task<ReconcileIndexResult> HandleAsync(
        ReconcileIndexCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var startedAt = timeProvider.GetTimestamp();
        var files = await catalog.ListSessionFilesAsync(cancellationToken).ConfigureAwait(false);
        ReconcileIndexLog.FilesDiscovered(logger, files.Count);

        var tally = new Tally();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await ReconcileOneAsync(file, command, tally, cancellationToken).ConfigureAwait(false);

            tally.Completed++;
            command.Progress?.Report(new ReconcileIndexProgress(tally.Completed, files.Count, file.SessionId, outcome));
        }

        tally.Pruned = await PruneAsync(files, cancellationToken).ConfigureAwait(false);
        await CompactIfAnythingChangedAsync(tally, cancellationToken).ConfigureAwait(false);
        await ReclaimSpaceIfRewrittenAsync(command, cancellationToken).ConfigureAwait(false);
        await writer.RecordReconcileAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return tally.ToResult(files.Count, timeProvider.GetElapsedTime(startedAt));
    }

    private async Task<ReconcileOutcome> ReconcileOneAsync(
        SessionFile file,
        ReconcileIndexCommand command,
        Tally tally,
        CancellationToken cancellationToken)
    {
        var request = new IndexSessionFileCommand { File = file, ForceFullReparse = command.ForceFullReparse };
        var result = await indexer.HandleAsync(request, cancellationToken).ConfigureAwait(false);

        tally.Absorb(result);

        if (result.ParseError is { } parseError)
        {
            ReconcileIndexLog.TranscriptPartiallyParsed(logger, file.FilePath, parseError);
        }

        return ToReconcileOutcome(result);
    }

    /// <summary>
    /// Maps the per-file outcome onto what a pass reports. A file that disappeared between being
    /// listed and being opened is reported as skipped rather than as a failure: the prune at the
    /// end of the pass is what removes it, and a machine in use is expected to lose files.
    /// </summary>
    private static ReconcileOutcome ToReconcileOutcome(IndexSessionFileResult result) => result.Outcome switch
    {
        IndexSessionFileOutcome.Skipped or IndexSessionFileOutcome.Missing => ReconcileOutcome.Skipped,
        IndexSessionFileOutcome.Failed => ReconcileOutcome.Failed,
        _ when result.ParseError is not null => ReconcileOutcome.IndexedWithParseError,
        _ => ReconcileOutcome.Indexed,
    };

    private async Task<int> PruneAsync(IReadOnlyList<SessionFile> files, CancellationToken cancellationToken)
    {
        var liveSessions = files.Select(file => file.SessionId).ToArray();
        var pruned = await writer.PruneMissingAsync(liveSessions, cancellationToken).ConfigureAwait(false);

        if (pruned > 0)
        {
            ReconcileIndexLog.SessionsPruned(logger, pruned);
        }

        return pruned;
    }

    /// <summary>
    /// Compacts the index only when the pass actually replaced something. A pass that skipped
    /// every transcript has left nothing behind to reclaim, and compaction is not free.
    /// </summary>
    private async Task CompactIfAnythingChangedAsync(Tally tally, CancellationToken cancellationToken)
    {
        if (tally.Indexed == 0 && tally.Pruned == 0)
        {
            return;
        }

        await writer.CompactAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the space a rewrite freed to the file system, and only after a rewrite.
    /// </summary>
    /// <remarks>
    /// A pass that read every transcript from the start replaces every session, which leaves the
    /// file holding as much free space as live data. Reclaiming it blocks every reader for the
    /// duration, so it is tied to the one pass a user asks for explicitly and is never done behind
    /// an incremental one.
    /// </remarks>
    private async Task ReclaimSpaceIfRewrittenAsync(
        ReconcileIndexCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.ForceFullReparse)
        {
            return;
        }

        await writer.VacuumAsync(cancellationToken).ConfigureAwait(false);
        ReconcileIndexLog.SpaceReclaimed(logger);
    }

    private sealed class Tally
    {
        public int Completed { get; set; }

        public int Indexed { get; private set; }

        public int Skipped { get; private set; }

        public int Pruned { get; set; }

        public int ChunksWritten { get; private set; }

        public long BytesRead { get; private set; }

        public int ParseErrors { get; private set; }

        public int Failed { get; private set; }

        public void Absorb(IndexSessionFileResult result)
        {
            ChunksWritten += result.ChunksWritten;
            BytesRead += result.BytesRead;

            if (result.ParseError is not null)
            {
                ParseErrors++;
            }

            switch (result.Outcome)
            {
                case IndexSessionFileOutcome.Skipped:
                case IndexSessionFileOutcome.Missing:
                    Skipped++;
                    break;
                case IndexSessionFileOutcome.Failed:
                    Failed++;
                    break;
                default:
                    Indexed++;
                    break;
            }
        }

        public ReconcileIndexResult ToResult(int filesDiscovered, TimeSpan elapsed) => new()
        {
            FilesDiscovered = filesDiscovered,
            SessionsIndexed = Indexed,
            SessionsSkipped = Skipped,
            SessionsPruned = Pruned,
            ChunksWritten = ChunksWritten,
            BytesRead = BytesRead,
            SessionsWithParseErrors = ParseErrors,
            FilesFailed = Failed,
            Elapsed = elapsed,
        };
    }
}
