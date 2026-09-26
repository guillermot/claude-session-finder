using Microsoft.Extensions.Logging;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Source-generated log messages for starting a program on the user's machine.
/// </summary>
/// <remarks>
/// An executable, its arguments and a working directory are paths and switches this application
/// built itself. None of it is transcript content, which is what keeps it out of the rule that
/// nothing a session said may reach a file that outlives the window.
/// </remarks>
internal static partial class ProcessLaunchLog
{
    [LoggerMessage(
        EventId = 1940,
        Level = LogLevel.Information,
        Message = "Starting {Executable} {Arguments} in {WorkingDirectory}.")]
    public static partial void Starting(
        ILogger logger,
        string executable,
        string arguments,
        string workingDirectory);

    [LoggerMessage(
        EventId = 1941,
        Level = LogLevel.Error,
        Message = "{Executable} could not be started.")]
    public static partial void StartRefused(ILogger logger, string executable, Exception exception);

    [LoggerMessage(
        EventId = 1942,
        Level = LogLevel.Warning,
        Message = "{Executable} exited with code {ExitCode} moments after being started.")]
    public static partial void ExitedImmediately(ILogger logger, string executable, int exitCode);
}
