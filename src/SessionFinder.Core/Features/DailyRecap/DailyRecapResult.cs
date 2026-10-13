namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// The answer to a <see cref="GetDailyRecapQuery"/>: one day in full, the days before it in brief,
/// and where the neighbouring days with activity are.
/// </summary>
public sealed record DailyRecapResult
{
    /// <summary>The working day the recap was asked for on.</summary>
    public required DateOnly Today { get; init; }

    /// <summary>
    /// The day the recap is about, or <see langword="null"/> when no earlier day has any activity.
    /// A day that was asked for by date is always present, with no projects when nothing happened.
    /// </summary>
    public DayRecap? Focus { get; init; }

    /// <summary>The active days before the focus day, the most recent first.</summary>
    public IReadOnlyList<DayRecap> Earlier { get; init; } = [];

    /// <summary>The closest active day before the focus day, for stepping back.</summary>
    public DateOnly? PreviousActiveDay { get; init; }

    /// <summary>The closest active day after the focus day, up to and including today.</summary>
    public DateOnly? NextActiveDay { get; init; }

    /// <summary>Whether there is anything at all to report.</summary>
    public bool HasActivity => Focus is { HasActivity: true } || Earlier.Count > 0;
}
