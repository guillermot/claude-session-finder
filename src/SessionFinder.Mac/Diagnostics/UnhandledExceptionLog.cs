using Microsoft.Extensions.Logging;

namespace SessionFinder.Mac.Diagnostics;

/// <summary>
/// Source-generated log messages for failures that reached the edge of the process.
/// </summary>
internal static partial class UnhandledExceptionLog
{
    /// <summary>Names the dispatcher as the origin of a failure, for the log.</summary>
    public const string OnUserInterfaceThread = "user interface thread";

    [LoggerMessage(
        EventId = 1710,
        Level = LogLevel.Error,
        Message = "An exception reached the {Origin} and was survived; the tray is still up.")]
    public static partial void Survived(ILogger logger, Exception exception, string origin);

    [LoggerMessage(
        EventId = 1711,
        Level = LogLevel.Critical,
        Message = "An exception is ending the process.")]
    public static partial void ProcessEnding(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1712,
        Level = LogLevel.Error,
        Message = "A task failed with nobody waiting on it.")]
    public static partial void UnobservedTaskFailed(ILogger logger, Exception exception);
}
