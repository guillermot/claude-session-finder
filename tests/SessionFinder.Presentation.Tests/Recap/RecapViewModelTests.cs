using NSubstitute;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;
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

        viewModel.Subheading.Should().Be("3 days ago · 1 session");
        viewModel.DisplayBody.Should().NotContain("**Fri, Oct 2**");
        viewModel.DisplayText.Should().StartWith("**Fri, Oct 2**");
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

        await _clipboard.Received(1).SetTextAsync(viewModel.RecapText, Arg.Any<CancellationToken>());
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
        viewModel.DisplayText.Should().Be(viewModel.RecapText);
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

        viewModel.DisplayText.Should().Be(viewModel.RecapText);
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
        dispatcher ?? new InlineUiDispatcher());

    private static DailyRecapResult RecapOf(DateOnly day, DateOnly? previous, DateOnly? next)
    {
        var at = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(-3));
        var session = new SessionRecap
        {
            SessionId = new SessionId(Guid.NewGuid()),
            Title = "Path tests",
            PromptCount = 3,
            FirstActivity = at,
            LastActivity = at.AddHours(1),
        };

        return new DailyRecapResult
        {
            Today = Today,
            Focus = new DayRecap(day,
            [
                new ProjectRecap
                {
                    Name = "finder",
                    Path = "/work/finder",
                    Sessions = [session],
                    FirstActivity = session.FirstActivity,
                    LastActivity = session.LastActivity,
                },
            ]),
            PreviousActiveDay = previous,
            NextActiveDay = next,
        };
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
