using Microsoft.Extensions.Logging;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.IndexSessionFile;

/// <summary>
/// Source-generated log messages for indexing one transcript.
/// </summary>
/// <remarks>
/// These run once per transcript, and during a watch they are the only account of what the indexer
/// did, which is why the plan is logged as well as the result. None of them carries transcript
/// text: session identifiers, paths, offsets and counters only.
/// </remarks>
internal static partial class IndexSessionFileLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Session {SessionId}: resuming at offset {ParseOffset} of {FileLength} byte(s).")]
    public static partial void ResumingTranscript(
        ILogger logger,
        SessionId sessionId,
        long parseOffset,
        long fileLength);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Session {SessionId}: reading all {FileLength} byte(s), because {Reason}.")]
    public static partial void ReadingWholeTranscript(
        ILogger logger,
        SessionId sessionId,
        FullReparseReason reason,
        long fileLength);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Session {SessionId}: wrote {ChunkCount} chunk(s), offset {ParseOffset}/{FileLength}.")]
    public static partial void TranscriptIndexed(
        ILogger logger,
        SessionId sessionId,
        int chunkCount,
        long parseOffset,
        long fileLength);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Session {SessionId}: appended {ChunkCount} chunk(s) from {BytesRead} new byte(s), offset now {ParseOffset}.")]
    public static partial void TranscriptAppended(
        ILogger logger,
        SessionId sessionId,
        int chunkCount,
        long bytesRead,
        long parseOffset);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Information,
        Message = "Session {SessionId}: title resolved again from {TitleSource}, offset now {ParseOffset}.")]
    public static partial void TitleRefreshed(
        ILogger logger,
        SessionId sessionId,
        TitleSource titleSource,
        long parseOffset);

    [LoggerMessage(
        EventId = 1105,
        Level = LogLevel.Warning,
        Message = "Transcript {FilePath} could not be read and was left as it was in the index.")]
    public static partial void TranscriptUnreadable(ILogger logger, Exception exception, string filePath);
}
