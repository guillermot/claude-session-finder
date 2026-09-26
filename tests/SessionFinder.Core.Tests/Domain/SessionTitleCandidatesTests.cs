using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Domain;

/// <summary>
/// Covers how a pass over the tail of a transcript combines with what an earlier pass knew.
/// </summary>
public sealed class SessionTitleCandidatesTests
{
    private const string FileName = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d.jsonl";
    private const string EarlierTitle = "Settlement";
    private const string LaterTitle = "Settlement mismatch, week 9";
    private const string FirstPrompt = "trace the settlement mismatch";

    [Fact]
    public void MergeWith_LaterCustomTitle_ReplacesTheEarlierOne()
    {
        var earlier = new SessionTitleCandidates { FileName = FileName, CustomTitle = EarlierTitle };
        var later = new SessionTitleCandidates { FileName = FileName, CustomTitle = LaterTitle };

        var merged = earlier.MergeWith(later);

        merged.CustomTitle.Should().Be(LaterTitle);
    }

    [Fact]
    public void MergeWith_LaterPassSawNoTitle_KeepsTheEarlierOne()
    {
        var earlier = new SessionTitleCandidates { FileName = FileName, AiTitle = EarlierTitle };
        var later = SessionTitleCandidates.ForFile(FileName);

        var merged = earlier.MergeWith(later);

        merged.AiTitle.Should().Be(EarlierTitle);
    }

    [Fact]
    public void MergeWith_LaterPassSawAPrompt_KeepsTheFirstPromptAlreadyKnown()
    {
        var earlier = new SessionTitleCandidates { FileName = FileName, FirstPrompt = FirstPrompt };
        var later = new SessionTitleCandidates { FileName = FileName, FirstPrompt = "a much later prompt" };

        var merged = earlier.MergeWith(later);

        merged.FirstPrompt.Should().Be(FirstPrompt);
    }

    [Fact]
    public void MergeWith_EarlierPassHadNoPrompt_TakesTheLaterOne()
    {
        var earlier = SessionTitleCandidates.ForFile(FileName);
        var later = new SessionTitleCandidates { FileName = FileName, FirstPrompt = FirstPrompt };

        var merged = earlier.MergeWith(later);

        merged.FirstPrompt.Should().Be(FirstPrompt);
    }
}
