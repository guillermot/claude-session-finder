using Microsoft.Extensions.Logging;

namespace SessionFinder.Mac.Diagnostics;

/// <summary>
/// Source-generated log messages for the start-up and shutdown of the Windows head.
/// </summary>
internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 1700,
        Level = LogLevel.Error,
        Message = "The host failed to start; the shell is staying up and the index will not be updated.")]
    public static partial void HostFailedToStart(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1701,
        Level = LogLevel.Error,
        Message = "The host failed to stop cleanly; the application is ending anyway.")]
    public static partial void HostFailedToStop(ILogger logger, Exception exception);
}
