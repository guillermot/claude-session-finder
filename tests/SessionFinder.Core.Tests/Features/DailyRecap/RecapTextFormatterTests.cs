using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;
using static SessionFinder.Core.Tests.Features.DailyRecap.RecapFixtures;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

public sealed class RecapTextFormatterTests
{
    [Fact]
    public void Format_AFocusDayAndAnEarlierDay_RendersTheDayInFullAndTheRestInBrief()
    {
        RecapTextFormatter.Format(FridayRecap()).Should().Be(
            """
            **Fri, Oct 2**
            - **finder** · `fix/paths` · 3 sessions · 10:05–17:40
              - Path tests (12 prompts)
              - CI actions (5 prompts)
              - _plus 1 quick question_
              - Commits:
                - `824d14a` Make path tests platform-neutral

            **Earlier**
            - **Thu, Oct 1**
              - finder: Security policy · 1 commit

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Format_Slack_UsesSingleAsterisksAndBulletCharacters()
    {
        var text = RecapTextFormatter.Format(FridayRecap(), RecapTextFormat.Slack);

        text.Should().StartWith("*Fri, Oct 2*\n• *finder* · `fix/paths`");
        text.Should().Contain("\n    ◦ Path tests (12 prompts)\n");
        text.Should().Contain("\n        ▪ `824d14a` Make path tests platform-neutral\n");
        text.Should().NotContain("**");
    }

    [Fact]
    public void Format_PlainText_HasNoMarkupAtAll()
    {
        var text = RecapTextFormatter.Format(FridayRecap(), RecapTextFormat.PlainText);

        text.Should().StartWith("Fri, Oct 2\n- finder · fix/paths");
        text.Should().NotContainAny("*", "`", "_plus");
        text.Should().Contain("  - plus 1 quick question\n");
    }

    [Fact]
    public void Format_AProjectWithOnlyQuickQuestions_CountsThemWithoutSayingPlus()
    {
        var recap = new DailyRecapResult
        {
            Today = Day(10, 5),
            Focus = new DayRecap(Day(10, 2), [Project("finder", [], [Recap("What is None?", 1, At(10, 2, 9), At(10, 2, 9))], [])]),
        };

        RecapTextFormatter.Format(recap).Should().Contain("  - _1 quick question_\n");
    }

    [Fact]
    public void Format_Week_RollsTheDaysUpByProject()
    {
        var text = RecapTextFormatter.Format(FridayRecap(), RecapTextFormat.Markdown, RecapScope.Week);

        text.Should().Be(
            """
            **Week of Thu, Oct 1 – Fri, Oct 2**
            - **finder** · `fix/paths` · 4 sessions over 2 days · 2 commits
              - Fri, Oct 2: Path tests; CI actions
              - Thu, Oct 1: Security policy
              - _plus 1 quick question_
              - Commits:
                - `824d14a` Make path tests platform-neutral
                - `41bc5b2` Add security policy

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Format_NothingBeforeToday_SaysSo()
    {
        var recap = new DailyRecapResult { Today = Day(10, 5) };

        RecapTextFormatter.Format(recap).Should().Contain("No Claude Code activity found before today");
    }

    [Fact]
    public void Format_WithPrompts_ListsTheKeyPromptsUnderEachSession()
    {
        var session = Recap("Path tests", 4, At(10, 4, 10), At(10, 4, 11)) with { KeyPrompts = ["make the tests neutral"] };
        var recap = new DailyRecapResult
        {
            Today = Day(10, 5),
            Focus = new DayRecap(Day(10, 4), [Project("finder", [], [session], [])]),
        };

        RecapTextFormatter.Format(recap, includePrompts: true).Should().Contain("    - \"make the tests neutral\"\n");
    }

    [Theory]
    [InlineData(5, "Today so far (Mon, Oct 5)")]
    [InlineData(4, "Yesterday (Sun, Oct 4)")]
    [InlineData(2, "Fri, Oct 2")]
    public void DescribeDay_NamesTodayAndYesterdayByThoseWords(int day, string expected)
    {
        RecapTextFormatter.DescribeDay(Day(10, day), Day(10, 5)).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void IsQuick_UpToTwoPrompts_IsAQuickQuestion(int prompts, bool expected)
    {
        Recap("A", prompts, At(10, 2, 9), At(10, 2, 9)).IsQuick.Should().Be(expected);
    }

    private static DailyRecapResult FridayRecap() => new()
    {
        Today = Day(10, 5),
        Focus = new DayRecap(Day(10, 2),
        [
            Project(
                "finder",
                ["fix/paths"],
                [
                    Recap("Path tests", 12, At(10, 2, 10, 5), At(10, 2, 12)),
                    Recap("What does rm -rf do?", 1, At(10, 2, 13), At(10, 2, 13)),
                    Recap("CI actions", 5, At(10, 2, 15), At(10, 2, 17, 40)),
                ],
                [new GitCommit("824d14a", "Make path tests platform-neutral", At(10, 2, 16))]),
        ]),
        Earlier =
        [
            new DayRecap(Day(10, 1),
            [
                Project("finder", [], [Recap("Security policy", 3, At(10, 1, 9), At(10, 1, 9))], [new GitCommit("41bc5b2", "Add security policy", At(10, 1, 9))]),
            ]),
        ],
    };

    private static SessionRecap Recap(string title, int prompts, DateTimeOffset first, DateTimeOffset last) => new()
    {
        SessionId = new SessionId(Guid.NewGuid()),
        Title = title,
        PromptCount = prompts,
        FirstActivity = first,
        LastActivity = last,
    };

    private static ProjectRecap Project(
        string name,
        IReadOnlyList<string> branches,
        IReadOnlyList<SessionRecap> sessions,
        IReadOnlyList<GitCommit> commits) => new()
    {
        Name = name,
        Path = "/work/" + name,
        Branches = branches,
        Sessions = sessions,
        Commits = commits,
        FirstActivity = sessions.Min(session => session.FirstActivity),
        LastActivity = sessions.Max(session => session.LastActivity),
    };
}
