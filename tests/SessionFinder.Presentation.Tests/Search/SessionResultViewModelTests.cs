using SessionFinder.Presentation.Search;

namespace SessionFinder.Presentation.Tests.Search;

public sealed class SessionResultViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_AFolderWithMixedCasing_KeepsTheCasingItWasFirstSeenWith()
    {
        var hit = SearchResults.Hit("Session", folder: @"C:\git\Project");

        var row = SessionResultViewModel.From(hit, Now);

        row.Folder.Should().Be(@"C:\git\Project");
    }

    [Fact]
    public void From_AnUnknownFolder_SaysSoRatherThanShowingABlank()
    {
        var hit = SearchResults.Hit("Session", folder: null!);

        var row = SessionResultViewModel.From(hit, Now);

        row.Folder.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void From_AnUnknownFolder_MarksTheRowAsHavingNoFolder()
    {
        var hit = SearchResults.Hit("Session", folder: null!);

        var row = SessionResultViewModel.From(hit, Now);

        row.IsFolderKnown.Should().BeFalse();
    }

    [Fact]
    public void From_ASnippetSpanningSeveralLines_CollapsesItOntoOne()
    {
        var hit = SearchResults.Hit("Session", snippet: "first line\r\n\r\n  second line");

        var row = SessionResultViewModel.From(hit, Now);

        row.Snippet.Should().Be("first line second line");
    }

    [Fact]
    public void From_NoSnippet_LeavesTheRowWithoutOne()
    {
        var hit = SearchResults.Hit("Session");

        var row = SessionResultViewModel.From(hit, Now);

        row.Snippet.Should().BeNull();
    }

    [Fact]
    public void From_ASessionTouchedMinutesAgo_DescribesTheAgeRatherThanTheTimestamp()
    {
        var hit = SearchResults.Hit("Session", lastActivity: Now.AddMinutes(-12));

        var row = SessionResultViewModel.From(hit, Now);

        row.LastActivity.Should().Be("12 minutes ago");
    }

    [Fact]
    public void From_ASessionTouchedAnHourAgo_UsesTheSingularForOne()
    {
        var hit = SearchResults.Hit("Session", lastActivity: Now.AddHours(-1));

        var row = SessionResultViewModel.From(hit, Now);

        row.LastActivity.Should().Be("1 hour ago");
    }

    [Fact]
    public void From_ASessionOlderThanAWeek_ShowsADate()
    {
        var hit = SearchResults.Hit("Session", lastActivity: Now.AddDays(-40));

        var row = SessionResultViewModel.From(hit, Now);

        row.LastActivity.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Fact]
    public void From_ASessionWithNoRecordedActivity_SaysTheAgeIsUnknown()
    {
        var hit = SearchResults.Hit("Session");

        var row = SessionResultViewModel.From(hit, Now);

        row.LastActivity.Should().Be("unknown");
    }
}
