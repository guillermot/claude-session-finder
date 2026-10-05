namespace SessionFinder.Core.Configuration;

/// <summary>
/// How the daily recap decides where a day begins, how far back it looks, and what it is allowed
/// to consult beyond the index.
/// </summary>
public sealed class RecapOptions
{
    /// <summary>Configuration section the options are bound from.</summary>
    public const string SectionName = "Finder:Recap";

    /// <summary>The local hour a working day starts at.</summary>
    public const int DefaultDayStartHour = 4;

    /// <summary>How many active days before the focus day the recap condenses.</summary>
    public const int DefaultLookbackDays = 4;

    /// <summary>The model alias the summary is written with.</summary>
    public const string DefaultSummaryModel = "haiku";

    /// <summary>
    /// The local hour a working day starts at. Work done before it counts toward the previous day,
    /// because a session that runs past midnight is the end of one day's work and not the start of
    /// the next one's.
    /// </summary>
    public int DayStartHour { get; set; } = DefaultDayStartHour;

    /// <summary>
    /// How many days before the focus day the recap condenses. These are days with activity, not
    /// calendar days, so a weekend or a holiday does not use them up.
    /// </summary>
    public int LookbackDays { get; set; } = DefaultLookbackDays;

    /// <summary>Whether the recap lists the user's own commits from the repositories worked in.</summary>
    public bool IncludeGit { get; set; } = true;

    /// <summary>
    /// Whether the window offers to have Claude write the recap as stand-up bullets. Off by default,
    /// because doing so sends prompt text out of the machine, which nothing else in the application
    /// ever does.
    /// </summary>
    public bool EnableAiSummary { get; set; }

    /// <summary>
    /// Absolute path of the <c>claude</c> executable. Blank searches the path and the places the
    /// installers put it, which a launcher started from the menu bar does not inherit on its path.
    /// </summary>
    public string? ClaudeExecutable { get; set; }

    /// <summary>The model the summary is written with, as an alias or a full name.</summary>
    public string SummaryModel { get; set; } = DefaultSummaryModel;
}
