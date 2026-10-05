using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.DailyRecap;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Recap;

/// <summary>
/// What the daily recap window shows: one day's recap as Markdown, the commands that step to the
/// neighbouring active days, and the optional summary written by Claude.
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

    private readonly IGetDailyRecapHandler _recapHandler;
    private readonly ISummarizeRecapHandler _summarizeHandler;
    private readonly IClipboardService _clipboard;
    private readonly IOptionsMonitor<RecapOptions> _options;
    private readonly AppCommandGuard _guard;
    private readonly IUiDispatcher _dispatcher;

    private DailyRecapResult? _recap;

    [ObservableProperty]
    private string _heading = EmptyHeading;

    [ObservableProperty]
    private string _subheading = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(DisplayBody))]
    private string _recapText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(DisplayBody))]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    [NotifyPropertyChangedFor(nameof(ToggleViewText))]
    private string? _summaryText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(DisplayBody))]
    [NotifyPropertyChangedFor(nameof(ToggleViewText))]
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
    public RecapViewModel(
        IGetDailyRecapHandler recapHandler,
        ISummarizeRecapHandler summarizeHandler,
        IClipboardService clipboard,
        IOptionsMonitor<RecapOptions> options,
        AppCommandGuard guard,
        IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(recapHandler);
        ArgumentNullException.ThrowIfNull(summarizeHandler);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _recapHandler = recapHandler;
        _summarizeHandler = summarizeHandler;
        _clipboard = clipboard;
        _options = options;
        _guard = guard;
        _dispatcher = dispatcher;
    }

    /// <summary>The text in the window: the summary when it is being shown, the recap otherwise.</summary>
    public string DisplayText => IsShowingSummary && SummaryText is { } summary ? summary : RecapText;

    /// <summary>
    /// What the window draws: the same text as <see cref="DisplayText"/>, minus the recap's opening
    /// line naming the day, which the window's own heading already shows.
    /// </summary>
    public string DisplayBody
    {
        get
        {
            if (IsShowingSummary && SummaryText is { } summary)
            {
                return summary;
            }

            var firstBreak = RecapText.IndexOf('\n', StringComparison.Ordinal);

            return _recap?.Focus is not null && firstBreak >= 0 ? RecapText[(firstBreak + 1)..] : RecapText;
        }
    }

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

        Heading = recap.Focus is { } focus ? RecapMarkdownFormatter.FormatDay(focus.Day) : EmptyHeading;
        Subheading = DescribeFocus(recap);
        RecapText = RecapMarkdownFormatter.Format(recap);
        SummaryText = null;
        IsShowingSummary = false;
        Status = null;
        CanSummarize = _options.CurrentValue.EnableAiSummary;

        RefreshCommands();
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

    private static string Count(int count, string noun) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

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
