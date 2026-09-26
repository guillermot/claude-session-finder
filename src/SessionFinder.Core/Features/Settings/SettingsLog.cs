using Microsoft.Extensions.Logging;

namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Source-generated log messages for the settings slice.
/// </summary>
/// <remarks>
/// None of these records a setting's value. The editor and terminal paths, and the index path, say
/// where a user keeps their work, and the log is a file that outlives the window.
/// </remarks>
internal static partial class SettingsLog
{
    [LoggerMessage(
        EventId = 1800,
        Level = LogLevel.Information,
        Message = "Settings saved.")]
    public static partial void SettingsSaved(ILogger logger);

    [LoggerMessage(
        EventId = 1801,
        Level = LogLevel.Warning,
        Message = "Settings were rejected before being written: {ErrorCode}.")]
    public static partial void SettingsRejected(ILogger logger, string errorCode);

    [LoggerMessage(
        EventId = 1802,
        Level = LogLevel.Error,
        Message = "The settings file could not be written.")]
    public static partial void SettingsNotSaved(ILogger logger, Exception exception);
}
