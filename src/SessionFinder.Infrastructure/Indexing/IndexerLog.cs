using Microsoft.Extensions.Logging;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexSessionFile;

namespace SessionFinder.Infrastructure.Indexing;

/// <summary>
/// Source-generated log messages for the background indexer.
/// </summary>
internal static partial class IndexerLog
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Full pass: {FileCount} file(s), {IndexedCount} indexed, {SkippedCount} skipped, {PrunedCount} pruned, {Seconds:N2} s.")]
    public static partial void FullPassCompleted(
        ILogger logger,
        int fileCount,
        int indexedCount,
        int skippedCount,
        int prunedCount,
        double seconds);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Change to session {SessionId}: {Outcome}, {ChunkCount} chunk(s) from {BytesRead} byte(s).")]
    public static partial void ChangeIndexed(
        ILogger logger,
        SessionId sessionId,
        IndexSessionFileOutcome outcome,
        int chunkCount,
        long bytesRead);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Information,
        Message = "Pruned {PrunedCount} session(s) whose transcript no longer exists.")]
    public static partial void SessionsPruned(ILogger logger, int prunedCount);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Information,
        Message = "Stopping with {PendingCount} change(s) still waiting; the next start will pick them up.")]
    public static partial void StopRequested(ILogger logger, int pendingCount);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Warning,
        Message = "The index could not be checkpointed on the way out.")]
    public static partial void CheckpointFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Error,
        Message = "The index at {DatabasePath} is not a usable database; it is being thrown away and rebuilt.")]
    public static partial void IndexCorrupt(ILogger logger, Exception exception, string databasePath);

    [LoggerMessage(
        EventId = 1306,
        Level = LogLevel.Information,
        Message = "The index was rebuilt from the transcripts on disk.")]
    public static partial void IndexRebuilt(ILogger logger);
}
