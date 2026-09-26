using Microsoft.Extensions.Logging;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Source-generated log messages for the start-at-sign-in registration.
/// </summary>
internal static partial class AutostartLog
{
    [LoggerMessage(
        EventId = 1910,
        Level = LogLevel.Information,
        Message = "Start at login disagreed with the setting and was rewritten to {IsEnabled}.")]
    public static partial void RegistrationRewritten(ILogger logger, bool isEnabled);

    [LoggerMessage(
        EventId = 1911,
        Level = LogLevel.Warning,
        Message = "Start at login could not be read or changed: {Reason}")]
    public static partial void RegistrationUnavailable(ILogger logger, string reason);
}
