namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Asks what was done on a working day and on the active days before it.
/// </summary>
public sealed record GetDailyRecapQuery
{
    /// <summary>A recap of the last active day before today, with the configured settings.</summary>
    public static GetDailyRecapQuery LastActiveDay { get; } = new();

    /// <summary>
    /// The day to report on. Left unset, the most recent day before today that has any activity is
    /// chosen, which is the day a stand-up asks about: Friday on a Monday, and the last day of work
    /// after a holiday.
    /// </summary>
    public DateOnly? Day { get; init; }

    /// <summary>How many active days before the focus day to condense. Left unset, the setting applies.</summary>
    public int? LookbackDays { get; init; }

    /// <summary>Whether to ask git for commits. Left unset, the setting applies.</summary>
    public bool? IncludeGit { get; init; }

    /// <summary>
    /// Builds a query for one particular day.
    /// </summary>
    /// <param name="day">The working day.</param>
    /// <returns>The query, with the configured settings.</returns>
    public static GetDailyRecapQuery For(DateOnly day) => new() { Day = day };
}
