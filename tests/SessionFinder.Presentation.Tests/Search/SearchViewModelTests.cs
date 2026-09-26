using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SessionFinder.Core.Features.Search;
using SessionFinder.Presentation.Actions;
using SessionFinder.Presentation.Search;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Search;

public sealed class SearchViewModelTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(150);

    [Fact]
    public async Task Query_TypedText_PublishesTheRankedRows()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("First session", "Second session"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.Results.Select(row => row.Title)
            .Should().Equal("First session", "Second session");
    }

    [Fact]
    public async Task Query_TypedText_SelectsTheFirstRow()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("First session", "Second session"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.SelectedResult!.Title.Should().Be("First session");
    }

    [Fact]
    public async Task Query_TypedText_ClearsBusyOnceTheResultsArrive()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("Session"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void Query_TypedText_ReportsBusyBeforeTheDelayElapses()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("Session"));

        fixture.ViewModel.Query = "deposit";

        fixture.ViewModel.IsBusy.Should().BeTrue();
    }

    [Fact]
    public async Task Query_NoSessionMatches_ReportsTheEmptyState()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching());

        fixture.ViewModel.Query = "nothing at all";
        await fixture.SettleAsync();

        fixture.ViewModel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Query_TheIndexThrows_ReportsAnErrorAndStaysOpen()
    {
        using var fixture = new Fixture();
        fixture.Handler.Throws(new InvalidOperationException("the index is locked"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Query_TheIndexThrows_DoesNotLeakTheExceptionMessageToTheUser()
    {
        using var fixture = new Fixture();
        fixture.Handler.Throws(new InvalidOperationException("the index is locked"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.ErrorMessage.Should().NotContain("the index is locked");
    }

    [Fact]
    public async Task Query_AStaleSearchCompletesAfterANewerOne_KeepsTheNewerRows()
    {
        using var fixture = new Fixture();
        var stale = new TaskCompletionSource<SearchSessionsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Handler.Answers((_, _) =>
        {
            staleStarted.TrySetResult();
            return stale.Task;
        });

        fixture.ViewModel.Query = "dep";
        var staleSearch = fixture.ViewModel.PendingSearch;
        fixture.Time.Advance(Delay);
        await staleStarted.Task;

        fixture.Handler.Answers(SearchResults.Matching("Newer session"));
        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        stale.SetResult(SearchResults.Matching("Stale session"));
        await staleSearch;

        fixture.ViewModel.Results.Select(row => row.Title).Should().Equal("Newer session");
    }

    [Fact]
    public async Task Query_ClearedAfterASearch_FallsBackToRecentSessions()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("Matched session"));
        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.Handler.Answers(SearchResults.Recent("Recent session"));
        fixture.ViewModel.Clear();
        await fixture.SettleAsync();

        fixture.ViewModel.IsShowingRecentSessions.Should().BeTrue();
    }

    [Fact]
    public async Task MoveSelection_AtTheBottomOfTheList_StaysOnTheLastRow()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("First", "Second"));
        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.MoveSelection(5);

        fixture.ViewModel.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public async Task MoveSelection_AtTheTopOfTheList_StaysOnTheFirstRow()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("First", "Second"));
        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.MoveSelection(-3);

        fixture.ViewModel.SelectedIndex.Should().Be(0);
    }

    [Fact]
    public void MoveSelection_NoResults_LeavesNothingSelected()
    {
        using var fixture = new Fixture();

        fixture.ViewModel.MoveSelection(1);

        fixture.ViewModel.SelectedIndex.Should().Be(-1);
    }

    [Fact]
    public async Task Publish_ResultsArrive_MarshalsThroughTheDispatcher()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("Session"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.Dispatcher.PostCount.Should().Be(1);
    }

    [Fact]
    public async Task Refresh_TheWindowIsReopened_RunsTheCurrentQueryAgain()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("Session"));
        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.Refresh();
        await fixture.SettleAsync();

        fixture.Handler.Queries.Should().Equal("deposit", "deposit");
    }

    [Fact]
    public async Task Query_TypedText_PointsTheActionsAtTheSelectedRow()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("First session", "Second session"));

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.Actions.Target!.Title.Should().Be("First session");
    }

    [Fact]
    public async Task MoveSelection_ADifferentRow_PointsTheActionsAtIt()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching("First session", "Second session"));
        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.MoveSelection(1);

        fixture.ViewModel.Actions.Target!.Title.Should().Be("Second session");
    }

    [Fact]
    public async Task Query_TextMatchingNothing_LeavesTheActionsWithNoTarget()
    {
        using var fixture = new Fixture();
        fixture.Handler.Answers(SearchResults.Matching());

        fixture.ViewModel.Query = "deposit";
        await fixture.SettleAsync();

        fixture.ViewModel.Actions.Target.Should().BeNull();
    }

    /// <summary>
    /// A view model wired to a controllable clock, a stub index and a dispatcher that runs inline,
    /// which together make the whole search pipeline deterministic.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Time = new FakeTimeProvider();
            Handler = new StubSearchSessionsHandler();
            Dispatcher = new InlineUiDispatcher();
            Handlers = new FakeSessionActionHandlers();
            ViewModel = new SearchViewModel(
                Handler,
                new SearchDebouncer(Time, Delay),
                Dispatcher,
                Time,
                new SessionActionsViewModel(
                    Handlers,
                    Handlers,
                    Handlers,
                    Handlers,
                    TestGuard.Over(new RecordingUserNotifier())),
                NullLogger<SearchViewModel>.Instance);
        }

        public FakeSessionActionHandlers Handlers { get; }

        public FakeTimeProvider Time { get; }

        public StubSearchSessionsHandler Handler { get; }

        public InlineUiDispatcher Dispatcher { get; }

        public SearchViewModel ViewModel { get; }

        public async Task SettleAsync()
        {
            Time.Advance(Delay);
            await ViewModel.PendingSearch;
        }

        public void Dispose() => ViewModel.Dispose();
    }
}
