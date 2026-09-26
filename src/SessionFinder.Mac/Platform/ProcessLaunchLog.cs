using Microsoft.Extensions.Logging;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Log messages for <see cref="MacShellLauncher"/>.
/// </summary>
internal static partial class ProcessLaunchLog
{
    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Information,
        Message = "Starting {Executable} {Arguments} in {WorkingDirectory}.")]
    public static partial void Starting(
        ILogger logger,
        string executable,
        string arguments,
        string workingDirectory);

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Error,
        Message = "{Executable} could not be started.")]
    public static partial void StartRefused(ILogger logger, string executable, Exception exception);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Warning,
        Message = "{Executable} exited with {ExitCode} immediately after starting.")]
    public static partial void ExitedImmediately(ILogger logger, string executable, int exitCode);
}
