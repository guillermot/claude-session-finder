using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Everything the settings window can change, in one value.
/// </summary>
/// <remarks>
/// <para>
/// This is the settings slice's own shape rather than the bound options classes. The options
/// classes are how configuration reaches the code that uses it — nullable, sectioned and filled in
/// by the binder — whereas this is what a user edits: complete, non-null and validated in one
/// place. Keeping them apart is what lets the window show a value the user never set without
/// writing it, and lets validation be a pure function.
/// </para>
/// <para>
/// The index path is not here. Changing it needs a restart, so it is reported by
/// <see cref="GetSettingsResult"/> and shown, not edited.
/// </para>
/// </remarks>
public sealed record FinderSettings
{
    private const int SmallestUsefulResultCount = 1;
    private const int LargestUsefulResultCount = 200;
    private const double SmallestUsefulHalfLifeDays = 0.1;
    private const int LatestUsefulDayStartHour = 12;
    private const int LargestUsefulLookbackDays = 14;

    /// <summary>The chord that summons the search box, such as <c>Ctrl+Alt+Space</c>.</summary>
    public required string Hotkey { get; init; }

    /// <summary>Whether the operating system starts the launcher when the user signs in.</summary>
    public required bool StartAtLogin { get; init; }

    /// <summary>Whether the search box hides as soon as it loses focus.</summary>
    public required bool HideOnDeactivate { get; init; }

    /// <summary>Absolute path of the editor executable, or blank to search the usual places.</summary>
    public required string? EditorPath { get; init; }

    /// <summary>Absolute path of the terminal executable, or blank to prefer the tabbed host.</summary>
    public required string? TerminalPath { get; init; }

    /// <summary>How many ranked sessions a search returns.</summary>
    public required int MaxResults { get; init; }

    /// <summary>Size of the recency bonus at zero age.</summary>
    public required double RecencyWeight { get; init; }

    /// <summary>How many days it takes for the recency bonus to decay by a factor of e.</summary>
    public required double RecencyHalfLifeDays { get; init; }

    /// <summary>How much a match counts for, by the kind of text it was found in.</summary>
    public required ChunkWeights ChunkWeights { get; init; }

    /// <summary>Whether the log records more than the ordinary account of what happened.</summary>
    public required bool VerboseLogging { get; init; }

    /// <summary>The local hour a working day starts at, for the daily recap.</summary>
    public required int RecapDayStartHour { get; init; }

    /// <summary>How many active days before the focus day the recap condenses.</summary>
    public required int RecapLookbackDays { get; init; }

    /// <summary>Whether the recap lists the user's own commits.</summary>
    public required bool RecapIncludeGit { get; init; }

    /// <summary>Whether the recap window offers to have Claude write the recap up.</summary>
    public required bool RecapAiSummary { get; init; }

    /// <summary>
    /// Builds the settings from the bound options, filling in anything the user never set.
    /// </summary>
    /// <param name="shell">The bound shell options.</param>
    /// <param name="search">The bound search options.</param>
    /// <param name="logLevel">The bound logging level.</param>
    /// <param name="recap">The bound daily recap options.</param>
    /// <returns>The settings as they currently apply.</returns>
    public static FinderSettings From(
        ShellOptions shell,
        SearchOptions search,
        LogLevelOptions logLevel,
        RecapOptions recap)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(logLevel);
        ArgumentNullException.ThrowIfNull(recap);

        return new FinderSettings
        {
            Hotkey = string.IsNullOrWhiteSpace(shell.Hotkey) ? ShellOptions.DefaultHotkey : shell.Hotkey,
            StartAtLogin = shell.StartAtLogin,
            HideOnDeactivate = shell.HideOnDeactivate,
            EditorPath = shell.EditorPath,
            TerminalPath = shell.TerminalPath,
            MaxResults = search.MaxResults,
            RecencyWeight = search.RecencyWeight,
            RecencyHalfLifeDays = search.RecencyHalfLifeDays,
            ChunkWeights = search.ToChunkWeights(),
            VerboseLogging = logLevel.IsVerbose,
            RecapDayStartHour = recap.DayStartHour,
            RecapLookbackDays = recap.LookbackDays,
            RecapIncludeGit = recap.IncludeGit,
            RecapAiSummary = recap.EnableAiSummary,
        };
    }

    /// <summary>
    /// Checks the values a user can get wrong.
    /// </summary>
    /// <remarks>
    /// Only the numbers are checked here. Whether a chord can actually be registered is a question
    /// about the machine rather than about the value, and it is answered by trying.
    /// </remarks>
    /// <returns>Success, or the first setting that would not work.</returns>
    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(Hotkey))
        {
            return Result.Failure(AppError.SettingRejected(nameof(Hotkey), "it is empty"));
        }

        if (MaxResults is < SmallestUsefulResultCount or > LargestUsefulResultCount)
        {
            return Result.Failure(AppError.SettingRejected(
                nameof(MaxResults),
                $"it must be between {SmallestUsefulResultCount} and {LargestUsefulResultCount}"));
        }

        if (RecencyHalfLifeDays < SmallestUsefulHalfLifeDays)
        {
            return Result.Failure(AppError.SettingRejected(
                nameof(RecencyHalfLifeDays),
                $"it must be at least {SmallestUsefulHalfLifeDays}"));
        }

        if (RecapDayStartHour is < 0 or > LatestUsefulDayStartHour)
        {
            return Result.Failure(AppError.SettingRejected(
                nameof(RecapDayStartHour),
                $"it must be between 0 and {LatestUsefulDayStartHour}"));
        }

        if (RecapLookbackDays is < 0 or > LargestUsefulLookbackDays)
        {
            return Result.Failure(AppError.SettingRejected(
                nameof(RecapLookbackDays),
                $"it must be between 0 and {LargestUsefulLookbackDays}"));
        }

        return ValidateWeights();
    }

    private Result ValidateWeights()
    {
        double[] weights =
        [
            RecencyWeight,
            ChunkWeights.Title,
            ChunkWeights.Folder,
            ChunkWeights.LastPrompt,
            ChunkWeights.UserPrompt,
            ChunkWeights.AssistantText,
        ];

        return weights.Any(weight => weight < 0 || double.IsNaN(weight))
            ? Result.Failure(AppError.SettingRejected("Weights", "no weight may be negative"))
            : Result.Success();
    }
}
