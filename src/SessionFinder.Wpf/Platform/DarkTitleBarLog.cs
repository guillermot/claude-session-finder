using Microsoft.Extensions.Logging;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Source-generated log messages for the dark title bar attribute.
/// </summary>
/// <remarks>
/// Recorded at debug level rather than as a warning: a title bar that stayed light is a window that
/// looks wrong, not a window that failed, and on an operating system too old for the attribute it
/// would be a warning on every open with nothing the user could do about it.
/// </remarks>
internal static partial class DarkTitleBarLog
{
    [LoggerMessage(
        EventId = 1930,
        Level = LogLevel.Debug,
        Message = "The desktop window manager refused the dark title bar attribute; the chrome stays light.")]
    public static partial void Unavailable(ILogger logger);
}
