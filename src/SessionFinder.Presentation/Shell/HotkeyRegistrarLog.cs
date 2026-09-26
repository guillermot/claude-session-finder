using Microsoft.Extensions.Logging;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Source-generated log messages for the hotkey registrar.
/// </summary>
internal static partial class HotkeyRegistrarLog
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Information,
        Message = "Hotkey registered: {Chord}.")]
    public static partial void HotkeyRegistered(ILogger logger, string chord);

    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Warning,
        Message = "Hotkey {Chord} is already taken; trying the next one.")]
    public static partial void HotkeyUnavailable(ILogger logger, string chord);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Warning,
        Message = "No hotkey could be registered out of {ChordCount} candidate(s); the tray menu is the way in.")]
    public static partial void NoHotkeyAvailable(ILogger logger, int chordCount);
}
