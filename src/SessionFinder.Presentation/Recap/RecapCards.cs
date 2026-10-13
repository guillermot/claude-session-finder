using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;

namespace SessionFinder.Presentation.Recap;

/// <summary>
/// One project's card in the recap window: its title row, its sessions, the quick questions folded
/// under them, and its commits.
/// </summary>
/// <remarks>
/// Quick questions and commits start folded. Quick questions are kept rather than dropped because
/// "what did I spend Tuesday afternoon on" sometimes has one for an answer, and commits are the
/// evidence behind the sessions rather than the story itself; both stay out of the way until asked
/// for, so a card reads as what was worked on and what came of it.
/// </remarks>
public sealed partial class ProjectCardViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuickToggleText))]
    private bool _isQuickExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommitsToggleText))]
    private bool _isCommitsExpanded;

    /// <summary>
    /// Builds a card.
    /// </summary>
    /// <param name="name">The project name, which is the card's title.</param>
    /// <param name="branches">The branches worked on, already joined for display.</param>
    /// <param name="span">When, or over how many days, the work happened.</param>
    /// <param name="sessions">The sessions that were real work.</param>
    /// <param name="quickSessions">The sessions that were quick questions.</param>
    /// <param name="commits">The commits.</param>
    public ProjectCardViewModel(
        string name,
        string branches,
        string span,
        IReadOnlyList<SessionRowViewModel> sessions,
        IReadOnlyList<SessionRowViewModel> quickSessions,
        IReadOnlyList<CommitRowViewModel> commits)
    {
        Name = name;
        Branches = branches;
        Span = span;
        Sessions = sessions;
        QuickSessions = quickSessions;
        Commits = commits;
    }

    /// <summary>The project name.</summary>
    public string Name { get; }

    /// <summary>The branches worked on, joined for display, or empty.</summary>
    public string Branches { get; }

    /// <summary>Whether there is a branch to show.</summary>
    public bool HasBranches => Branches.Length > 0;

    /// <summary>When the work happened, such as <c>09:12–14:47</c>, or <c>3 days</c> in a week.</summary>
    public string Span { get; }

    /// <summary>The sessions that were real work.</summary>
    public IReadOnlyList<SessionRowViewModel> Sessions { get; }

    /// <summary>Whether any session was real work.</summary>
    public bool HasSessions => Sessions.Count > 0;

    /// <summary>The sessions that were quick questions.</summary>
    public IReadOnlyList<SessionRowViewModel> QuickSessions { get; }

    /// <summary>Whether there are quick questions to fold.</summary>
    public bool HasQuickSessions => QuickSessions.Count > 0;

    /// <summary>The label of the row that folds and unfolds the quick questions.</summary>
    public string QuickToggleText => IsQuickExpanded
        ? "Hide quick questions"
        : RecapTextFormatter.Count(QuickSessions.Count, "quick question");

    /// <summary>The commits.</summary>
    public IReadOnlyList<CommitRowViewModel> Commits { get; }

    /// <summary>Whether there are commits to show.</summary>
    public bool HasCommits => Commits.Count > 0;

    /// <summary>The label of the row that folds and unfolds the commits.</summary>
    public string CommitsToggleText => IsCommitsExpanded
        ? "Hide commits"
        : RecapTextFormatter.Count(Commits.Count, "commit");

    /// <summary>Folds or unfolds the quick questions.</summary>
    [RelayCommand]
    public void ToggleQuick() => IsQuickExpanded = !IsQuickExpanded;

    /// <summary>Folds or unfolds the commits.</summary>
    [RelayCommand]
    public void ToggleCommits() => IsCommitsExpanded = !IsCommitsExpanded;
}

/// <summary>
/// One session row, and the three things that can be done to the session from it.
/// </summary>
public sealed class SessionRowViewModel
{

    /// <summary>
    /// Builds a row.
    /// </summary>
    /// <param name="session">The session's account of the day.</param>
    /// <param name="dayLabel">The day it belongs to, shown in a week; <see langword="null"/> in a day.</param>
    /// <param name="resume">Resumes the session in a terminal.</param>
    /// <param name="openFolder">Opens the session's folder in the editor.</param>
    /// <param name="reveal">Shows the session's folder in the file manager.</param>
    public SessionRowViewModel(
        SessionRecap session,
        string? dayLabel,
        Func<SessionRowViewModel, CancellationToken, Task> resume,
        Func<SessionRowViewModel, CancellationToken, Task> openFolder,
        Func<SessionRowViewModel, CancellationToken, Task> reveal)
    {
        ArgumentNullException.ThrowIfNull(session);

        Title = session.Title;
        Description = SessionDescription.For(session);
        PromptCount = session.PromptCount;
        SessionId = session.SessionId;
        Folder = session.Folder;
        DayLabel = dayLabel;

        ResumeCommand = new AsyncRelayCommand(token => resume(this, token), () => CanActOnFolder);
        OpenFolderCommand = new AsyncRelayCommand(token => openFolder(this, token), () => CanActOnFolder);
        RevealCommand = new AsyncRelayCommand(token => reveal(this, token), () => CanActOnFolder);
    }

    /// <summary>The session's title.</summary>
    public string Title { get; }

    /// <summary>
    /// A few lines under the title saying what the session achieved, written by
    /// <see cref="SessionDescription"/>.
    /// </summary>
    public string? Description { get; }

    /// <summary>Whether there is a description to show.</summary>
    public bool HasDescription => Description is not null;

    /// <summary>How many prompts it took.</summary>
    public int PromptCount { get; }

    /// <summary>The prompt count as the row shows it.</summary>
    public string PromptCountText => PromptCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The session, which resuming needs.</summary>
    public SessionId SessionId { get; }

    /// <summary>The folder the session ran in.</summary>
    public WorkingFolder Folder { get; }

    /// <summary>The day the session belongs to, in a week; <see langword="null"/> in a day.</summary>
    public string? DayLabel { get; }

    /// <summary>Whether the row shows its day.</summary>
    public bool HasDayLabel => DayLabel is not null;

    /// <summary>Whether a folder was recorded, without which none of the actions can run.</summary>
    public bool CanActOnFolder => Folder.IsKnown;

    /// <summary>Resumes the session in a terminal.</summary>
    public IAsyncRelayCommand ResumeCommand { get; }

    /// <summary>Opens the session's folder in the editor.</summary>
    public IAsyncRelayCommand OpenFolderCommand { get; }

    /// <summary>Shows the session's folder in the file manager.</summary>
    public IAsyncRelayCommand RevealCommand { get; }
}

/// <summary>
/// One commit row.
/// </summary>
/// <param name="Sha">The abbreviated hash.</param>
/// <param name="Subject">The first line of the message.</param>
/// <param name="When">The local time in a day, or the day in a week.</param>
public sealed record CommitRowViewModel(string Sha, string Subject, string When);

/// <summary>
/// One earlier day in the list under the cards, which opens that day when chosen.
/// </summary>
public sealed class EarlierDayRowViewModel
{
    /// <summary>
    /// Builds a row.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="projects">The projects worked on, joined for display.</param>
    /// <param name="commitCount">How many commits were made.</param>
    /// <param name="open">Loads the day.</param>
    public EarlierDayRowViewModel(DateOnly day, string projects, int commitCount, Func<DateOnly, CancellationToken, Task> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        Day = day;
        DayLabel = RecapTextFormatter.FormatDay(day);
        Projects = projects;
        CommitsText = commitCount > 0 ? RecapTextFormatter.Count(commitCount, "commit") : string.Empty;
        OpenCommand = new AsyncRelayCommand(token => open(day, token));
    }

    /// <summary>The day.</summary>
    public DateOnly Day { get; }

    /// <summary>The day as the row shows it, such as <c>Tue, Oct 6</c>.</summary>
    public string DayLabel { get; }

    /// <summary>The projects worked on that day.</summary>
    public string Projects { get; }

    /// <summary>How many commits were made, or empty when there were none.</summary>
    public string CommitsText { get; }

    /// <summary>Loads the day in the window.</summary>
    public IAsyncRelayCommand OpenCommand { get; }
}
