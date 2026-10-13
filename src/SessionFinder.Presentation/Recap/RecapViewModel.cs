using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.DailyRecap;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Recap;

/// <summary>
/// What the daily recap window shows: one card per project for a day or a week, the earlier days
/// to jump to, the commands that step between active days, and the optional summary by Claude.
/// </summary>
/// <remarks>
/// <para>
/// The window opens on the last active day before today, because that is the question a stand-up
/// asks. Stepping moves between days that had activity rather than calendar days, so a weekend is
/// one press, not two.
/// </para>
/// <para>
/// Everything that changes what the window shows is applied on the user interface thread. The
/// handlers finish on the thread pool, and a property changed there is a change the window never
/// sees, which is exactly how stepping between days once appeared to do nothing.
/// </para>
/// <para>
/// The cards are built from the structured recap, and the text that is copied is rendered from the
/// same recap in whichever format was last chosen, so what is on screen and what lands in a chat
/// window describe exactly the same work.
/// </para>
/// <para>
/// The summary command exists only while the setting is on. It is the one action in the
/// application that sends transcript text off the machine, so it is never one click away from a
/// user who has not chosen it.
/// </para>
/// </remarks>
public sealed partial class RecapViewModel : ObservableObject
{
    private const string LoadAction = "load recap";
    private const string LoadFailureTitle = "Could not build the recap";
    private const string CopyAction = "copy recap";
    private const string CopyFailureTitle = "Could not copy the recap";
    private const string SummarizeAction = "summarise recap";
    private const string SummarizeFailureTitle = "Could not write the summary";
    private const string EmptyHeading = "Daily recap";
    private const string CopiedStatus = "Copied to the clipboard.";
    private const string SummarizingStatus = "Asking Claude for a summary…";
    private const string ResumeAction = "resume session from recap";
    private const string ResumeFailureTitle = "Could not resume the session";
    private const string OpenFolderAction = "open folder from recap";
    private const string OpenFolderFailureTitle = "Could not open the folder";
    private const string RevealAction = "reveal folder from recap";
    private const string RevealFailureTitle = "Could not show the folder";
    private const string WeekDayFormat = "ddd d";
    private const string TimeFormat = "HH:mm";

    private readonly IGetDailyRecapHandler _recapHandler;
    private readonly ISummarizeRecapHandler _summarizeHandler;
    private readonly IClipboardService _clipboard;
    private readonly IOptionsMonitor<RecapOptions> _options;
    private readonly AppCommandGuard _guard;
    private readonly IUiDispatcher _dispatcher;
    private readonly IResumeSessionHandler _resume;
    private readonly IOpenFolderInEditorHandler _openFolder;
    private readonly IRevealInFileExplorerHandler _reveal;

    private DailyRecapResult? _recap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCards))]
    private IReadOnlyList<ProjectCardViewModel> _cards = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEarlierDays))]
    private IReadOnlyList<EarlierDayRowViewModel> _earlierDays = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDayView))]
    private bool _isWeekView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMarkdownFormat))]
    [NotifyPropertyChangedFor(nameof(IsSlackFormat))]
    [NotifyPropertyChangedFor(nameof(IsPlainTextFormat))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private RecapTextFormat _copyFormat = RecapTextFormat.Markdown;

    [ObservableProperty]
    private string? _emptyText;

    [ObservableProperty]
    private string _heading = EmptyHeading;

    [ObservableProperty]
    private string _subheading = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    [NotifyPropertyChangedFor(nameof(ToggleViewText))]
    private string? _summaryText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(ToggleViewText))]
    [NotifyPropertyChangedFor(nameof(IsShowingCards))]
    private bool _isShowingSummary;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canSummarize;

    [ObservableProperty]
    private string? _status;

    /// <summary>
    /// Builds the view model.
    /// </summary>
    /// <param name="recapHandler">Builds the recap for a day.</param>
    /// <param name="summarizeHandler">Has Claude write a recap up.</param>
    /// <param name="clipboard">Where the copy command puts the text.</param>
    /// <param name="options">Whether the summary command is offered.</param>
    /// <param name="guard">The seam every user-initiated action is reported through.</param>
    /// <param name="dispatcher">Brings finished work back to the user interface thread.</param>
    /// <param name="resume">Resumes a session from its row.</param>
    /// <param name="openFolder">Opens a session's folder in the editor.</param>
    /// <param name="reveal">Shows a session's folder in the file manager.</param>
    public RecapViewModel(
        IGetDailyRecapHandler recapHandler,
        ISummarizeRecapHandler summarizeHandler,
        IClipboardService clipboard,
        IOptionsMonitor<RecapOptions> options,
        AppCommandGuard guard,
        IUiDispatcher dispatcher,
        IResumeSessionHandler resume,
        IOpenFolderInEditorHandler openFolder,
        IRevealInFileExplorerHandler reveal)
    {
        ArgumentNullException.ThrowIfNull(recapHandler);
        ArgumentNullException.ThrowIfNull(summarizeHandler);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(openFolder);
        ArgumentNullException.ThrowIfNull(reveal);

        _recapHandler = recapHandler;
        _summarizeHandler = summarizeHandler;
        _clipboard = clipboard;
        _options = options;
        _guard = guard;
        _dispatcher = dispatcher;
        _resume = resume;
        _openFolder = openFolder;
        _reveal = reveal;
    }

    /// <summary>
    /// The text the copy command puts on the clipboard: the summary when it is being shown, and the
    /// recap of the day or week in the chosen format otherwise.
    /// </summary>
    public string DisplayText => IsShowingSummary && SummaryText is { } summary
        ? summary
        : _recap is { } recap
            ? RecapTextFormatter.Format(recap, CopyFormat, IsWeekView ? RecapScope.Week : RecapScope.Day)
            : string.Empty;

    /// <summary>Whether the day, rather than the week, is on screen.</summary>
    public bool IsDayView => !IsWeekView;

    /// <summary>Whether the cards, rather than the summary, are on screen.</summary>
    public bool IsShowingCards => !IsShowingSummary;

    /// <summary>Whether there is at least one card.</summary>
    public bool HasCards => Cards.Count > 0;

    /// <summary>Whether the list of earlier days has anything in it.</summary>
    public bool HasEarlierDays => EarlierDays.Count > 0;

    /// <summary>Whether the copy format is Markdown, for the menu's check mark.</summary>
    public bool IsMarkdownFormat => CopyFormat == RecapTextFormat.Markdown;

    /// <summary>Whether the copy format is Slack, for the menu's check mark.</summary>
    public bool IsSlackFormat => CopyFormat == RecapTextFormat.Slack;

    /// <summary>Whether the copy format is plain text, for the menu's check mark.</summary>
    public bool IsPlainTextFormat => CopyFormat == RecapTextFormat.PlainText;

    /// <summary>Whether a summary has been written for the day on screen.</summary>
    public bool HasSummary => SummaryText is not null;

    /// <summary>The label of the button that switches between the summary and the recap.</summary>
    public string ToggleViewText => IsShowingSummary ? "Show details" : "Show summary";

    /// <summary>
    /// Loads the recap of the last active day. Called every time the window opens, so it always
    /// reflects the index and the settings as they are now.
    /// </summary>
    /// <param name="cancellationToken">Abandons the load.</param>
    /// <returns>A task that completes once the recap is on screen.</returns>
    public Task LoadAsync(CancellationToken cancellationToken) =>
        LoadDayAsync(GetDailyRecapQuery.LastActiveDay, cancellationToken);

    /// <summary>Steps back to the closest earlier day with activity.</summary>
    /// <param name="cancellationToken">Abandons the load.</param>
    /// <returns>The running load.</returns>
    [RelayCommand(CanExecute = nameof(CanGoToPreviousDay))]
    public Task PreviousDayAsync(CancellationToken cancellationToken) =>
        _recap?.PreviousActiveDay is { } day
            ? LoadDayAsync(GetDailyRecapQuery.For(day), cancellationToken)
            : Task.CompletedTask;

    /// <summary>Steps forward to the closest later day with activity, up to today.</summary>
    /// <param name="cancellationToken">Abandons the load.</param>
    /// <returns>The running load.</returns>
    [RelayCommand(CanExecute = nameof(CanGoToNextDay))]
    public Task NextDayAsync(CancellationToken cancellationToken) =>
        _recap?.NextActiveDay is { } day
            ? LoadDayAsync(GetDailyRecapQuery.For(day), cancellationToken)
            : Task.CompletedTask;

    /// <summary>Puts the text on screen on the clipboard.</summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running copy.</returns>
    [RelayCommand]
    public async Task CopyAsync(CancellationToken cancellationToken)
    {
        var copied = await _guard
            .RunAsync(CopyAction, CopyFailureTitle, () => _clipboard.SetTextAsync(DisplayText, cancellationToken))
            .ConfigureAwait(true);

        OnUiThread(() => Status = copied ? CopiedStatus : null);
    }

    /// <summary>Copies in a format chosen from the menu, and remembers it for the next copy.</summary>
    /// <param name="format">The format to copy in.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running copy.</returns>
    [RelayCommand]
    public Task CopyAsAsync(RecapTextFormat format, CancellationToken cancellationToken)
    {
        CopyFormat = format;

        return CopyAsync(cancellationToken);
    }

    /// <summary>Switches between the day and the week rolled up by project.</summary>
    [RelayCommand]
    public void ToggleWeek() => IsWeekView = !IsWeekView;

    /// <summary>Has Claude write the day on screen up as stand-up bullets, and shows them.</summary>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>The running request.</returns>
    [RelayCommand(CanExecute = nameof(CanRunSummary))]
    public async Task SummarizeAsync(CancellationToken cancellationToken)
    {
        if (_recap is not { } recap)
        {
            return;
        }

        string? summary = null;

        SetBusy(true);
        Status = SummarizingStatus;

        try
        {
            await _guard
                .RunAsync(SummarizeAction, SummarizeFailureTitle, async () =>
                {
                    var result = await _summarizeHandler
                        .HandleAsync(new SummarizeRecapCommand(recap), cancellationToken)
                        .ConfigureAwait(true);

                    summary = result.IsSuccess ? result.Value : null;

                    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
                })
                .ConfigureAwait(true);
        }
        finally
        {
            OnUiThread(() =>
            {
                SetBusy(false);
                Status = null;

                if (summary is not null)
                {
                    SummaryText = summary;
                    IsShowingSummary = true;
                }
            });
        }
    }

    /// <summary>Switches between the summary and the structured recap.</summary>
    [RelayCommand]
    public void ToggleView() => IsShowingSummary = !IsShowingSummary && HasSummary;

    private async Task LoadDayAsync(GetDailyRecapQuery query, CancellationToken cancellationToken)
    {
        DailyRecapResult? loaded = null;

        SetBusy(true);

        try
        {
            await _guard
                .RunAsync(LoadAction, LoadFailureTitle, async () =>
                    loaded = await _recapHandler.HandleAsync(query, cancellationToken).ConfigureAwait(true))
                .ConfigureAwait(true);
        }
        finally
        {
            OnUiThread(() =>
            {
                SetBusy(false);

                if (loaded is not null)
                {
                    Show(loaded);
                }
            });
        }
    }

    /// <summary>Rebuilds what is on screen when the day/week switch flips.</summary>
    partial void OnIsWeekViewChanged(bool value)
    {
        if (_recap is { } recap)
        {
            Render(recap);
        }
    }

    private void OnUiThread(Action action)
    {
        if (_dispatcher.IsOnUiThread)
        {
            action();
            return;
        }

        _dispatcher.Post(action);
    }

    private void Show(DailyRecapResult recap)
    {
        _recap = recap;

        Render(recap);
        SummaryText = null;
        IsShowingSummary = false;
        Status = null;
        CanSummarize = _options.CurrentValue.EnableAiSummary;

        RefreshCommands();
    }

    /// <summary>
    /// Builds the heading, the cards and the earlier days for the day or the week, from the recap
    /// already in hand. Switching between the two never goes back to the index.
    /// </summary>
    private void Render(DailyRecapResult recap)
    {
        if (IsWeekView)
        {
            Heading = RecapTextFormatter.DescribeWeek(recap) ?? EmptyHeading;
            Subheading = DescribeWeek(recap);
            Cards = [.. RecapWeek.RollUp(recap).Select(BuildWeekCard)];
            EarlierDays = [];
        }
        else
        {
            Heading = recap.Focus is { } focus ? RecapTextFormatter.FormatDay(focus.Day) : EmptyHeading;
            Subheading = DescribeFocus(recap);
            Cards = [.. (recap.Focus?.Projects ?? []).Select(BuildDayCard)];
            EarlierDays = [.. recap.Earlier.Select(BuildEarlierDay)];
        }

        EmptyText = Cards.Count > 0
            ? null
            : recap.Focus is null ? "No Claude Code activity before today." : "No Claude Code activity on this day.";

        OnPropertyChanged(nameof(DisplayText));
    }

    private ProjectCardViewModel BuildDayCard(ProjectRecap project) => new(
        project.Name,
        string.Join(", ", project.Branches),
        RecapTextFormatter.FormatSpan(project.FirstActivity, project.LastActivity).Replace("–", " – ", StringComparison.Ordinal),
        [.. project.Sessions.Where(session => !session.IsQuick).Select(session => BuildRow(session, dayLabel: null))],
        [.. project.Sessions.Where(session => session.IsQuick).Select(session => BuildRow(session, dayLabel: null))],
        [.. project.Commits.Select(commit => new CommitRowViewModel(commit.Sha, commit.Subject, commit.Timestamp.ToString(TimeFormat, System.Globalization.CultureInfo.InvariantCulture)))]);

    private ProjectCardViewModel BuildWeekCard(WeekProjectRecap project) => new(
        project.Name,
        string.Join(", ", project.Branches),
        RecapTextFormatter.Count(project.ActiveDays, "day"),
        [.. project.Sessions.Where(entry => !entry.Session.IsQuick).Select(entry => BuildRow(entry.Session, WeekDay(entry.Day)))],
        [.. project.Sessions.Where(entry => entry.Session.IsQuick).Select(entry => BuildRow(entry.Session, WeekDay(entry.Day)))],
        [.. project.Commits.Select(entry => new CommitRowViewModel(entry.Commit.Sha, entry.Commit.Subject, WeekDay(entry.Day)))]);

    private EarlierDayRowViewModel BuildEarlierDay(DayRecap day) => new(
        day,
        session => BuildRow(session, dayLabel: null),
        (target, token) => LoadDayAsync(GetDailyRecapQuery.For(target), token));

    private SessionRowViewModel BuildRow(SessionRecap session, string? dayLabel) => new(
        session,
        dayLabel,
        (row, token) => _guard.RunAsync(
            ResumeAction,
            ResumeFailureTitle,
            () => _resume.HandleAsync(new ResumeSessionCommand(row.SessionId, row.Folder), token)),
        (row, token) => _guard.RunAsync(
            OpenFolderAction,
            OpenFolderFailureTitle,
            () => _openFolder.HandleAsync(new OpenFolderInEditorCommand(row.Folder), token)),
        (row, token) => _guard.RunAsync(
            RevealAction,
            RevealFailureTitle,
            () => _reveal.HandleAsync(new RevealInFileExplorerCommand(row.Folder), token)));

    private static string WeekDay(DateOnly day) => day.ToString(WeekDayFormat, System.Globalization.CultureInfo.InvariantCulture);

    private static string DescribeWeek(DailyRecapResult recap)
    {
        var days = RecapWeek.Days(recap);

        if (days.Count == 0)
        {
            return "No activity";
        }

        var sessions = days.Sum(day => day.Projects.Sum(project => project.Sessions.Count));
        var commits = days.Sum(day => day.Projects.Sum(project => project.Commits.Count));
        var parts = new List<string> { Count(days.Count, "active day"), Count(sessions, "session") };

        if (commits > 0)
        {
            parts.Add(Count(commits, "commit"));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Says where the day sits relative to today and how much happened in it, which is what the
    /// date alone does not: "Fri, Oct 2" needs "3 days ago" before it means anything on a Monday.
    /// </summary>
    private static string DescribeFocus(DailyRecapResult recap)
    {
        if (recap.Focus is not { } focus)
        {
            return "No Claude Code activity before today";
        }

        var age = recap.Today.DayNumber - focus.Day.DayNumber;
        var when = age switch
        {
            0 => "Today so far",
            1 => "Yesterday",
            < 0 => "Later",
            _ => $"{age} days ago",
        };

        if (!focus.HasActivity)
        {
            return $"{when} · no activity";
        }

        var sessions = focus.Projects.Sum(project => project.Sessions.Count);
        var commits = focus.Projects.Sum(project => project.Commits.Count);
        var parts = new List<string> { when, Count(sessions, "session") };

        if (commits > 0)
        {
            parts.Add(Count(commits, "commit"));
        }

        return string.Join(" · ", parts);
    }

    private static string Count(int count, string noun) => RecapTextFormatter.Count(count, noun);

    private void SetBusy(bool isBusy)
    {
        IsBusy = isBusy;
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        PreviousDayCommand.NotifyCanExecuteChanged();
        NextDayCommand.NotifyCanExecuteChanged();
        SummarizeCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoToPreviousDay() => !IsBusy && _recap?.PreviousActiveDay is not null;

    private bool CanGoToNextDay() => !IsBusy && _recap?.NextActiveDay is not null;

    private bool CanRunSummary() => !IsBusy && CanSummarize && _recap is { HasActivity: true };
}
