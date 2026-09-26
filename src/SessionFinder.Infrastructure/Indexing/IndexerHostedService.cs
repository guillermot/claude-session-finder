using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexSessionFile;
using SessionFinder.Core.Features.ReconcileIndex;
using SessionFinder.Infrastructure.FileSystem;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Indexing;

/// <summary>
/// Keeps the index in step with the transcripts while the application runs.
/// </summary>
/// <remarks>
/// <para>
/// Four parts in a line: the watcher reports paths, the buffer folds them per path and holds them
/// until they are worth acting on, a timer moves the ready ones onto a channel, and one task at the
/// far end does the writing. The last part is the constraint the rest is shaped around — the store
/// underneath permits exactly one writer — and the channel is what makes that a property of the
/// design rather than something to remember.
/// </para>
/// <para>
/// The watcher starts before the first pass rather than after it, so a transcript written during a
/// pass that takes seconds is not missed. What it reports in the meantime costs nothing: the path
/// collapses onto one buffer entry, and by the time it is indexed the pass has usually already
/// brought it up to date, which the watermark turns into a skip.
/// </para>
/// <para>
/// Shutdown is bounded by finishing the file in hand, never by finishing the queue. The writer
/// checks for it between files and never inside a transaction, so the worst case is one transcript
/// and what is left queued is picked up by the next start, which begins with a full pass anyway.
/// </para>
/// </remarks>
public sealed class IndexerHostedService(
    IReconcileIndexHandler reconcile,
    IIndexSessionFileHandler indexer,
    ISessionFileCatalog catalog,
    ISessionIndexWriter writer,
    SqliteIndexDatabase database,
    SessionFileWatcher watcher,
    PendingChangeBuffer buffer,
    IOptions<FinderOptions> options,
    TimeProvider timeProvider,
    ILogger<IndexerHostedService> logger) : BackgroundService
{
    private readonly FinderOptions _options = options.Value;

    private volatile bool _fullPassRequested;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        watcher.Changed += OnFileChanged;
        watcher.ChangesLost += OnChangesLost;

        var indexing = Task.Run(() => ConsumeAsync(channel.Reader, stoppingToken), CancellationToken.None);

        try
        {
            watcher.Start();
            await RunFullPassAsync(stoppingToken).ConfigureAwait(false);
            await FollowChangesAsync(channel.Writer, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            IndexerLog.StopRequested(logger, buffer.Count);
        }
        finally
        {
            watcher.Changed -= OnFileChanged;
            watcher.ChangesLost -= OnChangesLost;
            channel.Writer.TryComplete();

            await indexing.ConfigureAwait(false);
            await CheckpointAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Moves settled paths onto the channel on every tick, and runs a full pass when the watcher
    /// reports that it dropped notifications.
    /// </summary>
    private async Task FollowChangesAsync(ChannelWriter<string> queue, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (_fullPassRequested)
            {
                _fullPassRequested = false;
                await RunFullPassAsync(stoppingToken).ConfigureAwait(false);
            }

            foreach (var filePath in buffer.TakeReady(SettleFor, WaitAtMost))
            {
                await queue.WriteAsync(filePath, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The single writer. It takes the stopping token as a question asked between files rather than
    /// as one passed down, because a transaction interrupted halfway is worth less than a transcript
    /// read twice.
    /// </summary>
    private async Task ConsumeAsync(ChannelReader<string> queue, CancellationToken stoppingToken)
    {
        await foreach (var filePath in queue.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await SurvivingCorruptionAsync(
                () => IndexOneAsync(filePath, CancellationToken.None),
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task IndexOneAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!SessionId.TryParseFromFileName(filePath, out _))
        {
            return;
        }

        var file = await catalog.DescribeAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (file is null)
        {
            await PruneAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var command = new IndexSessionFileCommand { File = file };
        var result = await indexer.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        IndexerLog.ChangeIndexed(logger, file.SessionId, result.Outcome, result.ChunksWritten, result.BytesRead);

        if (result.Outcome == IndexSessionFileOutcome.Missing)
        {
            await PruneAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes the sessions whose transcripts are gone. The live set is re-enumerated rather than
    /// inferred from the notification, because a notification says one file changed and says nothing
    /// about the others.
    /// </summary>
    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        var files = await catalog.ListSessionFilesAsync(cancellationToken).ConfigureAwait(false);
        var live = files.Select(file => file.SessionId).ToArray();
        var pruned = await writer.PruneMissingAsync(live, cancellationToken).ConfigureAwait(false);

        if (pruned > 0)
        {
            IndexerLog.SessionsPruned(logger, pruned);
        }
    }

    private Task RunFullPassAsync(CancellationToken stoppingToken) =>
        SurvivingCorruptionAsync(() => ReconcileAsync(stoppingToken), stoppingToken);

    private async Task ReconcileAsync(CancellationToken stoppingToken)
    {
        var result = await reconcile
            .HandleAsync(ReconcileIndexCommand.Incremental, stoppingToken)
            .ConfigureAwait(false);

        IndexerLog.FullPassCompleted(
            logger,
            result.FilesDiscovered,
            result.SessionsIndexed,
            result.SessionsSkipped,
            result.SessionsPruned,
            result.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Runs indexing work and answers a corrupt index by throwing it away and doing the work again.
    /// </summary>
    /// <remarks>
    /// The index is a derived cache of files that are still on disk, so the only thing a corrupt
    /// one costs is the time of another pass. The alternative — letting the exception end the
    /// background service — leaves a running application whose index silently stops being updated,
    /// which is the failure this whole milestone exists to rule out. It is attempted once: a
    /// rebuild that is itself corrupt is a fault in something larger than this file.
    /// </remarks>
    private async Task SurvivingCorruptionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (SqliteException exception) when (SqliteIndexDatabase.IsCorruption(exception))
        {
            IndexerLog.IndexCorrupt(logger, exception, database.DatabasePath);

            await database.RebuildAsync(cancellationToken).ConfigureAwait(false);
            await work().ConfigureAwait(false);

            IndexerLog.IndexRebuilt(logger);
        }
    }

    /// <summary>
    /// Folds the write-ahead log back into the index on the way out, with its own token: the one
    /// that asked for the shutdown is already cancelled, and leaving the log unfolded is how a
    /// process that was stopped cleanly still looks half-written afterwards.
    /// </summary>
    private async Task CheckpointAsync()
    {
        try
        {
            await database.CheckpointAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            IndexerLog.CheckpointFailed(logger, exception);
        }
    }

    private void OnFileChanged(object? sender, SessionFileChangedEventArgs e) => buffer.Record(e.FilePath);

    private void OnChangesLost(object? sender, EventArgs e) => _fullPassRequested = true;

    private TimeSpan PollInterval => TimeSpan.FromMilliseconds(_options.ChangePollMilliseconds);

    private TimeSpan SettleFor => TimeSpan.FromMilliseconds(_options.ChangeSettleMilliseconds);

    private TimeSpan WaitAtMost => TimeSpan.FromMilliseconds(_options.ChangeMaximumWaitMilliseconds);
}
