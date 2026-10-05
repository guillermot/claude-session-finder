using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.DailyRecap;
using static SessionFinder.Core.Tests.Features.DailyRecap.RecapFixtures;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

public sealed class RecapMarkdownFormatterTests
{
    [Fact]
    public void Format_AFocusDayAndAnEarlierDay_RendersTheDayInFullAndTheRestInBrief()
    {
        var recap = new DailyRecapResult
        {
            Today = Day(10, 5),
            Focus = new DayRecap(Day(10, 2),
            [
                Project(
                    "finder",
                    ["fix/paths"],
                    [Recap("Path tests", 12, At(10, 2, 10, 5), At(10, 2, 12)), Recap("CI actions", 1, At(10, 2, 15), At(10, 2, 17, 40))],
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

        RecapMarkdownFormatter.Format(recap).Should().Be(
            """
            **Fri, Oct 2**
            - **finder** · `fix/paths` · 2 sessions · 10:05–17:40
              - Path tests (12 prompts)
              - CI actions (1 prompt)
              - Commits:
                - `824d14a` Make path tests platform-neutral

            **Earlier**
            - **Thu, Oct 1**
              - finder: Security policy · 1 commit

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Format_NothingBeforeToday_SaysSo()
    {
        var recap = new DailyRecapResult { Today = Day(10, 5) };

        RecapMarkdownFormatter.Format(recap).Should().Contain("No Claude Code activity found before today");
    }

    [Fact]
    public void Format_WithPrompts_ListsTheKeyPromptsUnderEachSession()
    {
        var session = Recap("Path tests", 2, At(10, 4, 10), At(10, 4, 11)) with { KeyPrompts = ["make the tests neutral"] };
        var recap = new DailyRecapResult
        {
            Today = Day(10, 5),
            Focus = new DayRecap(Day(10, 4), [Project("finder", [], [session], [])]),
        };

        RecapMarkdownFormatter.Format(recap, includePrompts: true).Should().Contain("    - \"make the tests neutral\"\n");
    }

    [Theory]
    [InlineData(5, "Today so far (Mon, Oct 5)")]
    [InlineData(4, "Yesterday (Sun, Oct 4)")]
    [InlineData(2, "Fri, Oct 2")]
    public void DescribeDay_NamesTodayAndYesterdayByThoseWords(int day, string expected)
    {
        RecapMarkdownFormatter.DescribeDay(Day(10, day), Day(10, 5)).Should().Be(expected);
    }

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
