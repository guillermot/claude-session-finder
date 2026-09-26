using Microsoft.Extensions.Logging;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Source-generated log messages for what the user was told.
/// </summary>
/// <remarks>
/// Notification text is written by this application, not read from a transcript, which is what
/// makes it safe to put in a file that outlives the window.
/// </remarks>
internal static partial class NotificationLog
{
    [LoggerMessage(
        EventId = 1920,
        Level = LogLevel.Information,
        Message = "Notice shown: {Title} — {Detail}")]
    public static partial void NoticeShown(ILogger logger, string title, string detail);

    [LoggerMessage(
        EventId = 1921,
        Level = LogLevel.Warning,
        Message = "Warning shown: {Title} — {Detail}")]
    public static partial void WarningShown(ILogger logger, string title, string detail);

    [LoggerMessage(
        EventId = 1922,
        Level = LogLevel.Error,
        Message = "Error shown: {Title} — {Detail}")]
    public static partial void ErrorShown(ILogger logger, string title, string detail);
}
