using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Rolls the focus day and the active days before it into one account per project, for a weekly
/// report or a retro.
/// </summary>
/// <remarks>
/// It is built from the recap that is already in hand rather than from a second query: the earlier
/// days of a recap carry their projects, sessions and commits in full, so a week is a regrouping of
/// what was read, not more reading. The same roll-up feeds the window and the copied text, so the
/// two can never disagree about what the week contained.
/// </remarks>
public static class RecapWeek
{
    /// <summary>
    /// The days the week covers, oldest first: the active earlier days and the focus day.
    /// </summary>
    /// <param name="recap">The recap.</param>
    /// <returns>The days with activity.</returns>
    public static IReadOnlyList<DayRecap> Days(DailyRecapResult recap)
    {
        ArgumentNullException.ThrowIfNull(recap);

        var days = recap.Earlier.Where(day => day.HasActivity).ToList();

        if (recap.Focus is { HasActivity: true } focus)
        {
            days.Add(focus);
        }

        return [.. days.OrderBy(day => day.Day)];
    }

    /// <summary>
    /// Groups the week's work by project, the busiest first.
    /// </summary>
    /// <param name="recap">The recap.</param>
    /// <returns>One entry per project worked on during the week.</returns>
    public static IReadOnlyList<WeekProjectRecap> RollUp(DailyRecapResult recap)
    {
        var entries = Days(recap)
            .SelectMany(day => day.Projects.Select(project => (day.Day, Project: project)))
            .GroupBy(entry => WorkingFolder.NormalizeKey(entry.Project.Path));

        return
        [
            .. entries
                .Select(BuildProject)
                .OrderByDescending(project => project.PromptCount)
                .ThenByDescending(project => project.LastDay),
        ];
    }

    private static WeekProjectRecap BuildProject(IEnumerable<(DateOnly Day, ProjectRecap Project)> group)
    {
        var entries = group.OrderByDescending(entry => entry.Day).ToList();
        var latest = entries[0].Project;

        return new WeekProjectRecap
        {
            Name = latest.Name,
            Path = latest.Path,
            Branches = [.. entries.SelectMany(entry => entry.Project.Branches).Distinct(StringComparer.Ordinal)],
            Sessions =
            [
                .. entries.SelectMany(entry => entry.Project.Sessions.Select(session => new DatedSession(entry.Day, session))),
            ],
            Commits =
            [
                .. entries.SelectMany(entry => entry.Project.Commits.Select(commit => new DatedCommit(entry.Day, commit))),
            ],
            ActiveDays = entries.Count,
        };
    }
}

/// <summary>
/// One project's week.
/// </summary>
public sealed record WeekProjectRecap
{
    /// <summary>The project's name.</summary>
    public required string Name { get; init; }

    /// <summary>The repository root or folder.</summary>
    public required string Path { get; init; }

    /// <summary>The branches worked on during the week.</summary>
    public IReadOnlyList<string> Branches { get; init; } = [];

    /// <summary>The sessions, the most recent day first.</summary>
    public required IReadOnlyList<DatedSession> Sessions { get; init; }

    /// <summary>The commits, the most recent day first.</summary>
    public IReadOnlyList<DatedCommit> Commits { get; init; } = [];

    /// <summary>On how many of the week's days the project was worked on.</summary>
    public required int ActiveDays { get; init; }

    /// <summary>How many prompts the project took over the week.</summary>
    public int PromptCount => Sessions.Sum(entry => entry.Session.PromptCount);

    /// <summary>The most recent day the project was worked on.</summary>
    public DateOnly LastDay => Sessions.Max(entry => entry.Day);
}

/// <summary>A session, with the day it belongs to.</summary>
/// <param name="Day">The working day.</param>
/// <param name="Session">The session's account of that day.</param>
public sealed record DatedSession(DateOnly Day, SessionRecap Session);

/// <summary>A commit, with the working day it was made on.</summary>
/// <param name="Day">The working day.</param>
/// <param name="Commit">The commit.</param>
public sealed record DatedCommit(DateOnly Day, GitCommit Commit);
