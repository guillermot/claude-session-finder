using Microsoft.Extensions.Logging;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Log messages for <see cref="CarbonGlobalHotkey"/>.
/// </summary>
internal static partial class CarbonHotkeyLog
{
    [LoggerMessage(
        EventId = 5300,
        Level = LogLevel.Information,
        Message = "The chord {Chord} was refused with status {Status}.")]
    public static partial void RegistrationFailed(ILogger logger, string chord, int status);

    [LoggerMessage(
        EventId = 5301,
        Level = LogLevel.Information,
        Message = "The key name {KeyName} is not one this platform recognises.")]
    public static partial void KeyNameNotRecognised(ILogger logger, string keyName);

    [LoggerMessage(
        EventId = 5302,
        Level = LogLevel.Error,
        Message = "The hotkey event handler could not be installed with status {Status}; no chord will be delivered.")]
    public static partial void HandlerNotInstalled(ILogger logger, int status);
}
