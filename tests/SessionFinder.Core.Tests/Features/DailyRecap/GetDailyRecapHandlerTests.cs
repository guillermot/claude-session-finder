using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.DailyRecap;
using SessionFinder.Core.Tests.Features.Search;
using static SessionFinder.Core.Tests.Features.DailyRecap.RecapFixtures;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

public sealed class GetDailyRecapHandlerTests
{
    private const string Repository = "/work/finder";

    private readonly InMemoryActivityReader _activity = new();
    private readonly StubGitActivityReader _git = new();
    private readonly RecapOptions _options = new();

    [Fact]
    public async Task HandleAsync_OnAMondayAfterAQuietWeekend_ReportsFriday()
    {
        _activity.Add(Session("Path tests", Repository, Prompt(At(10, 2, 10), "make the path tests platform neutral")));
        _activity.Add(Session("CI", Repository, Prompt(At(10, 1, 15), "update the CI actions")));

        var recap = await HandleAsync();

        recap.Focus!.Day.Should().Be(Day(10, 2));
        recap.Earlier.Should().ContainSingle().Which.Day.Should().Be(Day(10, 1));
    }

    [Fact]
    public async Task HandleAsync_TodayAlreadyHasActivity_StillReportsTheDayBeforeAndOffersTodayAsNext()
    {
        _activity.Add(Session("Yesterday's work", Repository, Prompt(At(10, 2, 10), "fix the reader")));
        _activity.Add(Session("Today's work", Repository, Prompt(At(10, 5, 9), "plan the recap feature")));

        var recap = await HandleAsync();

        recap.Focus!.Day.Should().Be(Day(10, 2));
        recap.NextActiveDay.Should().Be(Day(10, 5));
    }

    [Fact]
    public async Task HandleAsync_WorkPastMidnight_CountsTowardTheDayItStarted()
    {
        _activity.Add(Session(
            "Late night",
            Repository,
            Prompt(At(10, 2, 22), "start the migration"),
            Prompt(At(10, 3, 1, 30), "finish the migration")));

        var recap = await HandleAsync();

        recap.Focus!.Day.Should().Be(Day(10, 2));
        recap.Focus.Projects.Single().Sessions.Single().PromptCount.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_AnEmptyIndex_ReportsNothingRatherThanFailing()
    {
        var recap = await HandleAsync();

        recap.Focus.Should().BeNull();
        recap.HasActivity.Should().BeFalse();
        recap.Today.Should().Be(Day(10, 5));
    }

    [Fact]
    public async Task HandleAsync_ADayWithOnlyHarnessEnvelopes_IsNotAnActiveDay()
    {
        _activity.Add(Session("Real work", Repository, Prompt(At(10, 2, 10), "write the formatter")));
        _activity.Add(Session(
            "Resumed only",
            Repository,
            Prompt(At(10, 4, 10), "<command-name>/clear</command-name>"),
            Prompt(At(10, 4, 10, 5), "[Request interrupted by user]")));

        var recap = await HandleAsync();

        recap.Focus!.Day.Should().Be(Day(10, 2));
    }

    [Fact]
    public async Task HandleAsync_LookbackReachesPastTheFirstWindow_CountsActiveDaysNotCalendarDays()
    {
        _options.LookbackDays = 2;
        _activity.Add(Session("A", Repository, Prompt(At(10, 2, 10), "focus day")));
        _activity.Add(Session("B", Repository, Prompt(At(9, 30, 10), "two days before")));
        _activity.Add(Session("C", Repository, Prompt(At(9, 10, 10), "three weeks before")));
        _activity.Add(Session("D", Repository, Prompt(At(9, 1, 10), "a month before")));

        var recap = await HandleAsync();

        recap.Earlier.Select(day => day.Day).Should().Equal(Day(9, 30), Day(9, 10));
        recap.PreviousActiveDay.Should().Be(Day(9, 30));
    }

    [Fact]
    public async Task HandleAsync_ADayAskedForThatHadNoActivity_ReportsItAsEmpty()
    {
        _activity.Add(Session("A", Repository, Prompt(At(10, 2, 10), "focus day")));

        var recap = await HandleAsync(GetDailyRecapQuery.For(Day(10, 3)));

        recap.Focus!.Day.Should().Be(Day(10, 3));
        recap.Focus.HasActivity.Should().BeFalse();
        recap.PreviousActiveDay.Should().Be(Day(10, 2));
    }

    [Fact]
    public async Task HandleAsync_SessionsInARepositoryAndItsSubfolder_AreOneProjectWithItsCommits()
    {
        _git.HasRepository(Repository, Repository, Repository + "/src");
        _git.HasCommits(Repository, new GitCommit("824d14a", "Make path tests platform-neutral", At(10, 2, 16)));
        _activity.Add(Session("Root work", Repository, Prompt(At(10, 2, 10), "first")));
        _activity.Add(Session("Subfolder work", Repository + "/src", Prompt(At(10, 2, 11), "second")));

        var recap = await HandleAsync();

        var project = recap.Focus!.Projects.Should().ContainSingle().Subject;
        project.Name.Should().Be("finder");
        project.Sessions.Should().HaveCount(2);
        project.Commits.Should().ContainSingle().Which.Sha.Should().Be("824d14a");
    }

    [Fact]
    public async Task HandleAsync_CommitsOnAnotherDay_AreNotAttachedToTheFocusDay()
    {
        _git.HasRepository(Repository, Repository);
        _git.HasCommits(Repository, new GitCommit("aaaaaaa", "Thursday's commit", At(10, 1, 16)));
        _activity.Add(Session("Friday", Repository, Prompt(At(10, 2, 10), "work")));

        var recap = await HandleAsync(new GetDailyRecapQuery { LookbackDays = 0 });

        recap.Focus!.Projects.Single().Commits.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GitTurnedOff_NeverAsksGit()
    {
        _activity.Add(Session("A", Repository, Prompt(At(10, 2, 10), "work")));

        await HandleAsync(new GetDailyRecapQuery { IncludeGit = false });

        _git.RootLookups.Should().BeEmpty();
        _git.LogRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ADetachedCheckout_ShowsNoBranch()
    {
        _activity.Add(Session("A", Repository, branch: "HEAD", Prompt(At(10, 2, 10), "work")));

        var recap = await HandleAsync();

        recap.Focus!.Projects.Single().Branches.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ManyPrompts_PicksTheLongestDistinctOnesInTheOrderTheyWereTyped()
    {
        _activity.Add(Session(
            "A",
            Repository,
            Prompt(At(10, 2, 9), "a long prompt explaining the second feature in detail"),
            Prompt(At(10, 2, 10), "yes"),
            Prompt(At(10, 2, 11), "an even longer prompt explaining the first feature in great detail"),
            Prompt(At(10, 2, 12), "ok"),
            Prompt(At(10, 2, 13), "a medium prompt about the tests")));

        var recap = await HandleAsync();

        recap.Focus!.Projects.Single().Sessions.Single().KeyPrompts.Should().Equal(
            "a long prompt explaining the second feature in detail",
            "an even longer prompt explaining the first feature in great detail",
            "a medium prompt about the tests");
    }

    [Fact]
    public async Task HandleAsync_APromptSplitAcrossChunks_CountsAsOnePrompt()
    {
        var at = At(10, 2, 10);
        _activity.Add(Session("A", Repository, Prompt(at, "the first half of a long prompt"), Prompt(at, " and its second half")));

        var recap = await HandleAsync();

        var session = recap.Focus!.Projects.Single().Sessions.Single();
        session.PromptCount.Should().Be(1);
        session.KeyPrompts.Should().Equal("the first half of a long prompt and its second half");
    }

    [Fact]
    public async Task HandleAsync_TheFocusDay_CarriesTheAssistantsLastReplyAsTheOutcome()
    {
        _activity.Add(Session(
            "A",
            Repository,
            Prompt(At(10, 2, 10), "add the recap command"),
            Reply(At(10, 2, 10, 5), "Working on it."),
            Reply(At(10, 2, 10, 30), "Done: the recap command prints Markdown.")));

        var recap = await HandleAsync();

        recap.Focus!.Projects.Single().Sessions.Single().Outcome.Should().Be("Done: the recap command prints Markdown.");
    }

    [Fact]
    public async Task HandleAsync_AssistantText_IsOnlyReadForTheFocusDay()
    {
        _activity.Add(Session("A", Repository, Prompt(At(10, 2, 10), "work")));

        await HandleAsync();

        _activity.Calls.Where(call => call.IncludeAssistantText).Should().ContainSingle()
            .Which.From.Should().Be(At(10, 2, RecapOptions.DefaultDayStartHour));
    }

    [Fact]
    public async Task HandleAsync_TimesOnTheRecap_AreInTheUsersTimeZone()
    {
        _activity.Add(Session("A", Repository, Prompt(At(10, 2, 10, 15), "work")));

        var recap = await HandleAsync();

        var project = recap.Focus!.Projects.Single();
        project.FirstActivity.Offset.Should().Be(Offset);
        project.FirstActivity.Hour.Should().Be(10);
    }

    private Task<DailyRecapResult> HandleAsync(GetDailyRecapQuery? query = null) =>
        Handler().HandleAsync(query ?? GetDailyRecapQuery.LastActiveDay, CancellationToken.None);

    private GetDailyRecapHandler Handler()
    {
        var time = new FakeTimeProvider(Now);
        time.SetLocalTimeZone(Zone);

        return new GetDailyRecapHandler(
            _activity,
            _git,
            new FixedOptionsMonitor<RecapOptions>(_options),
            time,
            NullLogger<GetDailyRecapHandler>.Instance);
    }
}
