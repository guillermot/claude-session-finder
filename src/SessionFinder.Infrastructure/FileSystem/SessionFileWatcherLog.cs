using Microsoft.Extensions.Logging;

namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Source-generated log messages for the transcript watcher.
/// </summary>
internal static partial class SessionFileWatcherLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Watching {ProjectsDirectory} for transcript changes.")]
    public static partial void WatchStarted(ILogger logger, string projectsDirectory);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Warning,
        Message = "There is no {ProjectsDirectory} to watch; the index will not follow live sessions.")]
    public static partial void NothingToWatch(ILogger logger, string projectsDirectory);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Warning,
        Message = "Change notifications for {ProjectsDirectory} were dropped; a full pass will recover them.")]
    public static partial void NotificationsLost(ILogger logger, Exception exception, string projectsDirectory);
}
