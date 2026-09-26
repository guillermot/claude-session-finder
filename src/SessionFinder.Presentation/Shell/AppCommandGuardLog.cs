using Microsoft.Extensions.Logging;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Source-generated log messages for the command guard.
/// </summary>
/// <remarks>
/// The name of the action is a constant chosen by the caller, never a query, a title or a path the
/// user typed. Everything a transcript contains stays out of the log file.
/// </remarks>
internal static partial class AppCommandGuardLog
{
    [LoggerMessage(
        EventId = 1900,
        Level = LogLevel.Warning,
        Message = "The action {Action} was refused: {ErrorCode}.")]
    public static partial void CommandRefused(ILogger logger, string action, string errorCode);

    [LoggerMessage(
        EventId = 1901,
        Level = LogLevel.Error,
        Message = "The action {Action} threw instead of returning a result.")]
    public static partial void CommandThrew(ILogger logger, string action, Exception exception);
}
