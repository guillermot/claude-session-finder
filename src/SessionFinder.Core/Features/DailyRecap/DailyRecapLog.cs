using Microsoft.Extensions.Logging;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Source-generated log messages for the daily recap.
/// </summary>
/// <remarks>
/// Counters and days only. The recap is made of prompt text, session titles and commit messages,
/// and none of them belongs in a file that outlives the window.
/// </remarks>
internal static partial class DailyRecapLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Recap for {FocusDay}: {ProjectCount} project(s), {SessionCount} session(s), {CommitCount} commit(s), {EarlierDayCount} earlier day(s).")]
    public static partial void RecapBuilt(
        ILogger logger,
        DateOnly focusDay,
        int projectCount,
        int sessionCount,
        int commitCount,
        int earlierDayCount);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Recap: no activity found before {Today}.")]
    public static partial void NothingToRecap(ILogger logger, DateOnly today);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Recap summary written ({Length} character(s)).")]
    public static partial void SummaryWritten(ILogger logger, int length);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Recap summary could not be written: {ErrorCode}.")]
    public static partial void SummaryFailed(ILogger logger, string errorCode);
}
