using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Domain;

/// <summary>
/// Covers the four-level precedence and the rule that matters most in practice: title records are
/// appended to a transcript long after the messages, so resolving again with newer candidates has
/// to be able to change the answer.
/// </summary>
public sealed class SessionTitleTests
{
    private const string FileName = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d.jsonl";
    private const string CustomTitle = "Settlement mismatch, week 9";
    private const string GeneratedTitle = "Settlement mismatch trace";
    private const string FirstPrompt = "trace the settlement mismatch";

    [Fact]
    public void Resolve_CustomTitlePresent_WinsOverEveryOtherCandidate()
    {
        var candidates = new SessionTitleCandidates
        {
            FileName = FileName,
            CustomTitle = CustomTitle,
            AiTitle = GeneratedTitle,
            FirstPrompt = FirstPrompt,
        };

        var title = SessionTitle.Resolve(candidates);

        title.Should().Be(new SessionTitle(CustomTitle, TitleSource.CustomTitle));
    }

    [Fact]
    public void Resolve_NoCustomTitle_FallsBackToTheGeneratedTitle()
    {
        var candidates = new SessionTitleCandidates
        {
            FileName = FileName,
            AiTitle = GeneratedTitle,
            FirstPrompt = FirstPrompt,
        };

        var title = SessionTitle.Resolve(candidates);

        title.Should().Be(new SessionTitle(GeneratedTitle, TitleSource.AiTitle));
    }

    [Fact]
    public void Resolve_OnlyAPrompt_FallsBackToTheFirstPrompt()
    {
        var candidates = new SessionTitleCandidates { FileName = FileName, FirstPrompt = FirstPrompt };

        var title = SessionTitle.Resolve(candidates);

        title.Should().Be(new SessionTitle(FirstPrompt, TitleSource.FirstPrompt));
    }

    [Fact]
    public void Resolve_NoCandidateAtAll_FallsBackToTheFileName()
    {
        var candidates = SessionTitleCandidates.ForFile(FileName);

        var title = SessionTitle.Resolve(candidates);

        title.Should().Be(new SessionTitle(FileName, TitleSource.FileName));
    }

    [Fact]
    public void Resolve_EveryCandidateBlank_ProducesAPlaceholder()
    {
        var candidates = new SessionTitleCandidates { FileName = "   ", CustomTitle = "", AiTitle = "  " };

        var title = SessionTitle.Resolve(candidates);

        title.Source.Should().Be(TitleSource.Unknown);
    }

    [Fact]
    public void Resolve_MultilinePrompt_CollapsesIntoOneLine()
    {
        var candidates = new SessionTitleCandidates
        {
            FileName = FileName,
            FirstPrompt = "  trace the\r\n\r\n  settlement\tmismatch  ",
        };

        var title = SessionTitle.Resolve(candidates);

        title.Text.Should().Be(FirstPrompt);
    }

    [Fact]
    public void Resolve_PromptLongerThanTheLimit_IsTruncatedWithAnEllipsis()
    {
        var candidates = new SessionTitleCandidates
        {
            FileName = FileName,
            FirstPrompt = new string('a', SessionTitle.MaxPromptTitleLength + 20),
        };

        var title = SessionTitle.Resolve(candidates);

        title.Text.Should().Be(new string('a', SessionTitle.MaxPromptTitleLength) + "…");
    }

    [Fact]
    public void Resolve_AfterALateCustomTitleIsMerged_ChangesItsAnswer()
    {
        var firstPass = new SessionTitleCandidates { FileName = FileName, FirstPrompt = FirstPrompt };
        var secondPass = new SessionTitleCandidates { FileName = FileName, CustomTitle = CustomTitle };

        var title = SessionTitle.Resolve(firstPass.MergeWith(secondPass));

        title.Should().Be(new SessionTitle(CustomTitle, TitleSource.CustomTitle));
    }
}
