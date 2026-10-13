using NSubstitute;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Recap;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Recap;

public sealed class RecapViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);
    private static readonly DateOnly Friday = new(2026, 10, 2);
    private static readonly DateOnly Thursday = new(2026, 10, 1);

    private readonly IGetDailyRecapHandler _recaps = Substitute.For<IGetDailyRecapHandler>();
    private readonly ISummarizeRecapHandler _summaries = Substitute.For<ISummarizeRecapHandler>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly RecordingUserNotifier _notifier = new();
    private readonly RecapOptions _options = new();
    private readonly IResumeSessionHandler _resume = Substitute.For<IResumeSessionHandler>();
    private readonly IOpenFolderInEditorHandler _openFolder = Substitute.For<IOpenFolderInEditorHandler>();
    private readonly IRevealInFileExplorerHandler _reveal = Substitute.For<IRevealInFileExplorerHandler>();

    public RecapViewModelTests()
    {
        _recaps.HandleAsync(Arg.Is<GetDailyRecapQuery>(query => query.Day == null), Arg.Any<CancellationToken>())
            .Returns(RecapOf(Friday, previous: Thursday, next: Today));
        _recaps.HandleAsync(Arg.Is<GetDailyRecapQuery>(query => query.Day == Thursday), Arg.Any<CancellationToken>())
            .Returns(RecapOf(Thursday, previous: null, next: Friday));
        _clipboard.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    [Fact]
    public async Task LoadAsync_TheLastActiveDayWasFriday_HeadsTheWindowWithIt()
    {
        var viewModel = ViewModel();

        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.Heading.Should().Be("Fri, Oct 2");
        viewModel.DisplayText.Should().Contain("Path tests");
    }

    [Fact]
    public async Task LoadAsync_FridayOnAMonday_SaysHowLongAgoAndHowMuchHappened()
    {
        var viewModel = ViewModel();

        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.Subheading.Should().Be("3 days ago · 2 sessions · 1 commit");
        viewModel.DisplayText.Should().StartWith("**Fri, Oct 2**");
    }

    [Fact]
    public async Task LoadAsync_ADay_BuildsOneCardPerProjectWithQuickQuestionsFolded()
    {
        var viewModel = ViewModel();

        await viewModel.LoadAsync(CancellationToken.None);

        var card = viewModel.Cards.Should().ContainSingle().Subject;
        card.Name.Should().Be("finder");
        card.Branches.Should().Be("main");
        card.Span.Should().Be("10:00 – 12:00");
        card.Sessions.Select(row => row.Title).Should().Equal("Path tests");
        card.QuickSessions.Select(row => row.Title).Should().Equal("Quick one");
        card.IsQuickExpanded.Should().BeFalse();
        card.QuickToggleText.Should().Be("1 quick question");
        card.Commits.Should().ContainSingle().Which.Should().Be(new CommitRowViewModel("abc1234", "Commit on Path tests", "10:30"));
    }

    [Fact]
    public async Task ToggleQuick_OnACard_UnfoldsTheQuickQuestions()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);
        var card = viewModel.Cards[0];

        card.ToggleQuickCommand.Execute(null);

        card.IsQuickExpanded.Should().BeTrue();
        card.QuickToggleText.Should().Be("Hide quick questions");
    }

    [Fact]
    public async Task ToggleCommits_OnACard_StartsFoldedAndUnfolds()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);
        var card = viewModel.Cards[0];

        card.IsCommitsExpanded.Should().BeFalse();
        card.CommitsToggleText.Should().Be("1 commit");
        card.ToggleCommitsCommand.Execute(null);

        card.IsCommitsExpanded.Should().BeTrue();
        card.CommitsToggleText.Should().Be("Hide commits");
    }

    [Theory]
    [InlineData("Done: invoices now stream.", "the longest prompt", "Done: invoices now stream.")]
    [InlineData(null, "the longest prompt", "the longest prompt")]
    [InlineData(null, null, null)]
    [InlineData("Behind the `applePay` flag, **done**.", null, "Behind the applePay flag, done.")]
    public void SessionRow_Description_PrefersTheOutcomeAndFallsBackToTheKeyPrompt(string? outcome, string? prompt, string? expected)
    {
        var session = new SessionRecap
        {
            SessionId = new SessionId(Guid.NewGuid()),
            Title = "A",
            PromptCount = 4,
            FirstActivity = DateTimeOffset.UnixEpoch,
            LastActivity = DateTimeOffset.UnixEpoch,
            Outcome = outcome,
            KeyPrompts = prompt is null ? [] : [prompt],
        };

        var row = new SessionRowViewModel(session, null, NoOp, NoOp, NoOp);

        row.Description.Should().Be(expected);
        row.HasDescription.Should().Be(expected is not null);
    }

    private static Task NoOp(SessionRowViewModel row, CancellationToken token) => Task.CompletedTask;

    [Fact]
    public async Task EarlierDay_ChosenFromTheList_LoadsThatDay()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        var row = viewModel.EarlierDays.Should().ContainSingle().Subject;
        row.Projects.Should().Be("docs");
        row.CommitsText.Should().Be("1 commit");
        await row.OpenCommand.ExecuteAsync(null);

        viewModel.Heading.Should().Be("Thu, Oct 1");
    }

    [Fact]
    public async Task ToggleWeek_RollsTheDaysUpByProjectWithoutGoingBackToTheIndex()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);
        _recaps.ClearReceivedCalls();

        viewModel.ToggleWeekCommand.Execute(null);

        viewModel.Heading.Should().Be("Week of Thu, Oct 1 – Fri, Oct 2");
        viewModel.Cards.Select(card => card.Name).Should().BeEquivalentTo("finder", "docs");
        viewModel.Cards.Single(card => card.Name == "docs").Sessions.Single().DayLabel.Should().Be("Thu 1");
        viewModel.EarlierDays.Should().BeEmpty();
        viewModel.DisplayText.Should().StartWith("**Week of");
        await _recaps.DidNotReceiveWithAnyArgs().HandleAsync(default!, default);
    }

    [Fact]
    public async Task CopyAs_Slack_CopiesSlackMarkupAndRemembersTheChoice()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.CopyAsCommand.ExecuteAsync(RecapTextFormat.Slack);
        await viewModel.CopyCommand.ExecuteAsync(null);

        viewModel.IsSlackFormat.Should().BeTrue();
        await _clipboard.Received(2).SetTextAsync(Arg.Is<string>(text => text.StartsWith("*Fri, Oct 2*")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeFromARow_AsksTheHandlerWithThatSessionAndFolder()
    {
        _resume.HandleAsync(Arg.Any<ResumeSessionCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);
        var row = viewModel.Cards[0].Sessions[0];

        await row.ResumeCommand.ExecuteAsync(null);

        await _resume.Received(1).HandleAsync(
            Arg.Is<ResumeSessionCommand>(command => command.SessionId == row.SessionId && command.Folder.Display == "/work/finder"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevealFromARow_ShowsTheSessionsFolder()
    {
        _reveal.HandleAsync(Arg.Any<RevealInFileExplorerCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.Cards[0].Sessions[0].RevealCommand.ExecuteAsync(null);

        await _reveal.Received(1).HandleAsync(
            Arg.Is<RevealInFileExplorerCommand>(command => command.Folder.Display == "/work/finder"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviousDay_FromFriday_LoadsThursdayAndThenHasNothingFurtherBack()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.PreviousDayCommand.ExecuteAsync(null);

        viewModel.Heading.Should().Be("Thu, Oct 1");
        viewModel.PreviousDayCommand.CanExecute(null).Should().BeFalse();
        viewModel.NextDayCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task PreviousDay_TheLoadFinishesOffTheUserInterfaceThread_AppliesItThroughTheDispatcher()
    {
        var dispatcher = new QueuedUiDispatcher();
        var viewModel = ViewModel(dispatcher);
        await viewModel.LoadAsync(CancellationToken.None);
        dispatcher.RunPending();

        await viewModel.PreviousDayCommand.ExecuteAsync(null);

        viewModel.Heading.Should().Be("Fri, Oct 2");
        dispatcher.RunPending();
        viewModel.Heading.Should().Be("Thu, Oct 1");
    }

    [Fact]
    public async Task Copy_TheRecapIsShowing_PutsItOnTheClipboard()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.CopyCommand.ExecuteAsync(null);

        await _clipboard.Received(1).SetTextAsync(Arg.Is<string>(text => text.StartsWith("**Fri, Oct 2**")), Arg.Any<CancellationToken>());
        viewModel.Status.Should().Be("Copied to the clipboard.");
    }

    [Fact]
    public async Task Summarize_TheSettingIsOff_IsNotOffered()
    {
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        viewModel.CanSummarize.Should().BeFalse();
        viewModel.SummarizeCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Summarize_TheSettingIsOn_ShowsTheSummaryAndCopiesIt()
    {
        _options.EnableAiSummary = true;
        _summaries.HandleAsync(Arg.Any<SummarizeRecapCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("- shipped the path tests"));
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.SummarizeCommand.ExecuteAsync(null);
        await viewModel.CopyCommand.ExecuteAsync(null);

        viewModel.DisplayText.Should().Be("- shipped the path tests");
        await _clipboard.Received(1).SetTextAsync("- shipped the path tests", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Summarize_ClaudeFails_TellsTheUserAndKeepsTheRecap()
    {
        _options.EnableAiSummary = true;
        _summaries.HandleAsync(Arg.Any<SummarizeRecapCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(AppError.SummarizerNotFound));
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);

        await viewModel.SummarizeCommand.ExecuteAsync(null);

        _notifier.Notifications.Should().ContainSingle().Which.Message.Should().Be(AppError.SummarizerNotFound.Message);
        viewModel.HasSummary.Should().BeFalse();
        viewModel.DisplayText.Should().StartWith("**");
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleView_AfterASummary_SwitchesBackToTheDetails()
    {
        _options.EnableAiSummary = true;
        _summaries.HandleAsync(Arg.Any<SummarizeRecapCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("- summary"));
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);
        await viewModel.SummarizeCommand.ExecuteAsync(null);

        viewModel.ToggleViewCommand.Execute(null);

        viewModel.DisplayText.Should().StartWith("**");
        viewModel.ToggleViewText.Should().Be("Show summary");
    }

    [Fact]
    public async Task PreviousDay_AfterASummary_DropsTheSummaryOfTheOtherDay()
    {
        _options.EnableAiSummary = true;
        _summaries.HandleAsync(Arg.Any<SummarizeRecapCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("- friday summary"));
        var viewModel = ViewModel();
        await viewModel.LoadAsync(CancellationToken.None);
        await viewModel.SummarizeCommand.ExecuteAsync(null);

        await viewModel.PreviousDayCommand.ExecuteAsync(null);

        viewModel.HasSummary.Should().BeFalse();
        viewModel.DisplayText.Should().NotContain("friday summary");
    }

    private RecapViewModel ViewModel(IUiDispatcher? dispatcher = null) => new(
        _recaps,
        _summaries,
        _clipboard,
        new MutableOptionsMonitor<RecapOptions>(_options),
        TestGuard.Over(_notifier),
        dispatcher ?? new InlineUiDispatcher(),
        _resume,
        _openFolder,
        _reveal);

    private static DailyRecapResult RecapOf(DateOnly day, DateOnly? previous, DateOnly? next) => new()
    {
        Today = Today,
        Focus = DayOf(day, "finder", "Path tests"),
        Earlier = previous is { } earlier ? [DayOf(earlier, "docs", "Migration guide")] : [],
        PreviousActiveDay = previous,
        NextActiveDay = next,
    };

    private static DayRecap DayOf(DateOnly day, string project, string title)
    {
        var at = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(-3));
        SessionRecap[] sessions =
        [
            new()
            {
                SessionId = new SessionId(Guid.NewGuid()),
                Folder = WorkingFolder.FromTranscriptCwd("/work/" + project),
                Title = title,
                PromptCount = 3,
                FirstActivity = at,
                LastActivity = at.AddHours(1),
            },
            new()
            {
                SessionId = new SessionId(Guid.NewGuid()),
                Folder = WorkingFolder.FromTranscriptCwd("/work/" + project),
                Title = "Quick one",
                PromptCount = 1,
                FirstActivity = at.AddHours(2),
                LastActivity = at.AddHours(2),
            },
        ];

        return new DayRecap(day,
        [
            new ProjectRecap
            {
                Name = project,
                Path = "/work/" + project,
                Branches = ["main"],
                Sessions = sessions,
                Commits = [new GitCommit("abc1234", "Commit on " + title, at.AddMinutes(30))],
                FirstActivity = at,
                LastActivity = at.AddHours(2),
            },
        ]);
    }

    /// <summary>
    /// A dispatcher for a test running off the user interface thread: work posted to it waits
    /// until the test runs it, which is what shows whether the view model marshalled at all.
    /// </summary>
    private sealed class QueuedUiDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _pending = new();

        public bool IsOnUiThread => false;

        public void Post(Action action) => _pending.Enqueue(action);

        public void RunPending()
        {
            while (_pending.TryDequeue(out var action))
            {
                action();
            }
        }
    }
}
