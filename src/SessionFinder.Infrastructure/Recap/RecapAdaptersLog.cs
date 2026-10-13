using Microsoft.Extensions.Logging;

namespace SessionFinder.Infrastructure.Recap;

/// <summary>
/// Source-generated log messages for the programs the recap asks: git and Claude.
/// </summary>
/// <remarks>
/// Neither the prompt, the summary nor a commit message is ever logged. The folder a repository
/// lives in is, at debug level only, because a git lookup that fails for one folder is otherwise
/// impossible to diagnose.
/// </remarks>
internal static partial class RecapAdaptersLog
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Debug,
        Message = "git {Verb} in {Folder} did not answer: exit {ExitCode}, timed out {TimedOut}, not started: {StartError}.")]
    public static partial void GitDidNotAnswer(
        ILogger logger,
        string verb,
        string folder,
        int exitCode,
        bool timedOut,
        string? startError);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Asking {Executable} for a recap summary with model {Model}.")]
    public static partial void SummaryRequested(ILogger logger, string executable, string model);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "The recap summary failed: exit {ExitCode}, timed out {TimedOut}, not started: {StartError}.")]
    public static partial void SummaryProcessFailed(ILogger logger, int exitCode, bool timedOut, string? startError);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Warning,
        Message = "No claude executable was found for the recap summary.")]
    public static partial void SummarizerMissing(ILogger logger);
}
