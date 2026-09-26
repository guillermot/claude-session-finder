using Microsoft.Extensions.Logging;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Log messages for <see cref="OsaScriptNotifier"/>.
/// </summary>
internal static partial class MacNotificationLog
{
    [LoggerMessage(
        EventId = 5200,
        Level = LogLevel.Debug,
        Message = "The notification could not be shown and was only written to the log.")]
    public static partial void NotShown(ILogger logger, Exception exception);
}
