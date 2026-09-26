using Microsoft.Extensions.Logging;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Source-generated log messages for the system-wide hotkey adapter.
/// </summary>
internal static partial class Win32HotkeyLog
{
    [LoggerMessage(
        EventId = 1600,
        Level = LogLevel.Warning,
        Message = "Chord {Chord} is already owned by another application.")]
    public static partial void ChordAlreadyOwned(ILogger logger, string chord);

    [LoggerMessage(
        EventId = 1601,
        Level = LogLevel.Warning,
        Message = "Chord {Chord} could not be registered; Windows reported error {ErrorCode}.")]
    public static partial void RegistrationFailed(ILogger logger, string chord, int errorCode);

    [LoggerMessage(
        EventId = 1602,
        Level = LogLevel.Warning,
        Message = "Key name '{KeyName}' is not one this platform recognises.")]
    public static partial void KeyNameNotRecognised(ILogger logger, string keyName);
}
