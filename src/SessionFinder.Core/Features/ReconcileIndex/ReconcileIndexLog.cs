using Microsoft.Extensions.Logging;

namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// Source-generated log messages for the reconcile pass.
/// </summary>
/// <remarks>
/// These run once per transcript, which is why they are generated rather than interpolated. None
/// of them carries transcript text: session identifiers, paths, offsets and counters only.
/// </remarks>
internal static partial class ReconcileIndexLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Reconcile found {FileCount} transcript file(s).")]
    public static partial void FilesDiscovered(ILogger logger, int fileCount);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Transcript {FilePath} indexed with damaged lines skipped: {ParseError}")]
    public static partial void TranscriptPartiallyParsed(ILogger logger, string filePath, string parseError);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Pruned {PrunedCount} session(s) whose transcript no longer exists.")]
    public static partial void SessionsPruned(ILogger logger, int prunedCount);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Reclaimed the space freed by rewriting every session.")]
    public static partial void SpaceReclaimed(ILogger logger);
}
