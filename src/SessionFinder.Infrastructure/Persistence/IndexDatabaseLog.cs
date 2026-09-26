using Microsoft.Extensions.Logging;

namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// Source-generated log messages for the index file itself.
/// </summary>
/// <remarks>
/// Throwing the index away is cheap and correct, and it must never be silent: the next start looks
/// exactly like a first one, and without these two lines there would be nothing to tell a user who
/// noticed the long pass why it happened.
/// </remarks>
internal static partial class IndexDatabaseLog
{
    [LoggerMessage(
        EventId = 1350,
        Level = LogLevel.Error,
        Message = "The index at {DatabasePath} could not be opened as a database and is being deleted.")]
    public static partial void IndexUnusable(ILogger logger, Exception exception, string databasePath);

    [LoggerMessage(
        EventId = 1351,
        Level = LogLevel.Warning,
        Message = "An empty index was created at {DatabasePath}; the next pass reads every transcript.")]
    public static partial void IndexRecreated(ILogger logger, string databasePath);
}
