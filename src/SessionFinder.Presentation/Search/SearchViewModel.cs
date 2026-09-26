using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SessionFinder.Core.Features.Search;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Actions;

namespace SessionFinder.Presentation.Search;

/// <summary>
/// What the search box shows: the query, the ranked rows, the selection, and which of the busy,
/// empty and failed states it is in.
/// </summary>
/// <remarks>
/// <para>
/// This type never names a dispatcher. It is handed one, and it uses it in exactly one place — the
/// line where a result computed on a background thread becomes an observable property. Everything
/// else runs wherever it was called from, which is what lets the whole search pipeline be asserted
/// without a message loop.
/// </para>
/// <para>
/// A failed search leaves the previous rows on screen rather than clearing them. The index is a
/// cache being rewritten by another part of the same process, so a transient failure is a moment,
/// and throwing away results the user was reading is a worse answer than saying so under them.
/// </para>
/// </remarks>
public sealed partial class SearchViewModel : ObservableObject, IDisposable
{
    private const string SearchFailedMessage = "The index could not be searched. The log has the detail.";

    private readonly ISearchSessionsHandler _handler;
    private readonly SearchDebouncer _debouncer;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SearchViewModel> _logger;
    private readonly CancellationTokenSource _lifetime = new();

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isShowingRecentSessions = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedResult))]
    private int _selectedIndex = -1;

    /// <summary>
    /// Builds the view model.
    /// </summary>
    /// <param name="handler">Runs the searches.</param>
    /// <param name="debouncer">Decides when typing has paused and which answer may be shown.</param>
    /// <param name="dispatcher">The way back to the user interface thread.</param>
    /// <param name="timeProvider">The clock the row timestamps are described against.</param>
    /// <param name="actions">What can be done to whichever row is selected.</param>
    /// <param name="logger">Where a failed search is recorded.</param>
    public SearchViewModel(
        ISearchSessionsHandler handler,
        SearchDebouncer debouncer,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider,
        SessionActionsViewModel actions,
        ILogger<SearchViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(debouncer);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(logger);

        _handler = handler;
        _debouncer = debouncer;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _logger = logger;

        Actions = actions;
    }

    /// <summary>The ranked rows, best first.</summary>
    public ObservableCollection<SessionResultViewModel> Results { get; } = [];

    /// <summary>
    /// What can be done to the selected row. The search box owns the selection, so it is also what
    /// keeps this pointed at the right session.
    /// </summary>
    public SessionActionsViewModel Actions { get; }

    /// <summary>
    /// The search that is scheduled or running. It completes when that search has published, been
    /// superseded or been cancelled, and it is what a caller awaits to know the list has settled.
    /// </summary>
    public Task PendingSearch { get; private set; } = Task.CompletedTask;

    /// <summary>The row the keyboard is on, or <see langword="null"/> when the list is empty.</summary>
    public SessionResultViewModel? SelectedResult =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    /// <summary>
    /// Runs the current query again, which is what the window does when it opens: the index is
    /// being written to by the same process, so the answer from a minute ago may be stale.
    /// </summary>
    public void Refresh() => RequestSearch();

    /// <summary>
    /// Empties the search box, which falls back to listing the most recently active sessions.
    /// </summary>
    public void Clear()
    {
        if (Query.Length == 0)
        {
            Refresh();
            return;
        }

        Query = string.Empty;
    }

    /// <summary>
    /// Moves the selection by a number of rows, stopping at either end.
    /// </summary>
    /// <param name="offset">How far to move; negative moves towards the top.</param>
    public void MoveSelection(int offset)
    {
        if (Results.Count == 0)
        {
            SelectedIndex = -1;
            return;
        }

        SelectedIndex = Math.Clamp(SelectedIndex + offset, 0, Results.Count - 1);
    }

    /// <summary>Cancels anything in flight and stops accepting further searches.</summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        _debouncer.Dispose();
        _lifetime.Dispose();
    }

    partial void OnQueryChanged(string value) => RequestSearch();

    partial void OnSelectedIndexChanged(int value) => Actions.Target = SelectedResult;

    private void RequestSearch()
    {
        var text = Query;

        IsBusy = true;
        ErrorMessage = null;

        PendingSearch = _debouncer.ScheduleAsync(
            token => RunAsync(text, token),
            Apply,
            _lifetime.Token);
    }

    private async Task<SearchOutcome> RunAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _handler
                .HandleAsync(SearchSessionsQuery.For(text), cancellationToken)
                .ConfigureAwait(false);

            return SearchOutcome.For(result, _timeProvider.GetLocalNow());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            SearchViewModelLog.SearchFailed(_logger, exception);
            return SearchOutcome.Failed;
        }
    }

    /// <summary>
    /// The single point where background work becomes user interface state, and therefore the only
    /// place in the whole view model that the dispatcher appears.
    /// </summary>
    private void Apply(SearchOutcome outcome) => _dispatcher.Post(() => Publish(outcome));

    private void Publish(SearchOutcome outcome)
    {
        IsBusy = false;

        if (outcome.HasFailed)
        {
            ErrorMessage = SearchFailedMessage;
            IsEmpty = false;
            return;
        }

        Results.Clear();

        foreach (var row in outcome.Rows)
        {
            Results.Add(row);
        }

        ErrorMessage = null;
        IsShowingRecentSessions = outcome.IsRecentFallback;
        IsEmpty = Results.Count == 0;
        SelectedIndex = Results.Count == 0 ? -1 : 0;
        OnPropertyChanged(nameof(SelectedResult));

        Actions.Target = SelectedResult;
    }

    /// <summary>
    /// What one search produced, in the shape the rows are published from. Building the rows on the
    /// background thread keeps the dispatcher callback down to assignments.
    /// </summary>
    private sealed record SearchOutcome
    {
        /// <summary>The outcome of a search that threw.</summary>
        public static SearchOutcome Failed { get; } = new()
        {
            Rows = [],
            IsRecentFallback = false,
            HasFailed = true,
        };

        /// <summary>The rows to show.</summary>
        public required IReadOnlyList<SessionResultViewModel> Rows { get; init; }

        /// <summary>Whether the rows are recent sessions rather than matches.</summary>
        public required bool IsRecentFallback { get; init; }

        /// <summary>Whether the search threw rather than answering.</summary>
        public required bool HasFailed { get; init; }

        /// <summary>
        /// Projects a ranked result into rows.
        /// </summary>
        /// <param name="result">What the handler answered.</param>
        /// <param name="now">The moment row timestamps are described against.</param>
        /// <returns>The outcome.</returns>
        public static SearchOutcome For(SearchSessionsResult result, DateTimeOffset now) => new()
        {
            Rows = [.. result.Hits.Select(hit => SessionResultViewModel.From(hit, now))],
            IsRecentFallback = result.IsRecentFallback,
            HasFailed = false,
        };
    }
}
