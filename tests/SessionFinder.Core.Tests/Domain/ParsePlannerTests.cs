using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Domain;

/// <summary>
/// Covers the skip / resume / reparse decision. Both failure modes are silent in production — too
/// eager to skip and new messages never appear, too eager to reparse and every pass re-reads the
/// whole corpus — so every branch gets a test.
/// </summary>
public sealed class ParsePlannerTests
{
    private const long StoredSize = 4_096;
    private const long StoredTicks = 638_000_000_000_000_000;
    private const long StoredOffset = 4_000;
    private const string StoredHash = "AABBCC";
    private const string DifferentHash = "DDEEFF";

    [Fact]
    public void Decide_FileWasNeverIndexed_ReadsItFromTheStart()
    {
        var plan = ParsePlanner.Decide(null, Stored());

        plan.Should().Be(new ParsePlan.FullReparse(FullReparseReason.NeverIndexed));
    }

    [Fact]
    public void Decide_SizeAndTimestampUnchanged_SkipsTheFile()
    {
        var plan = ParsePlanner.Decide(Stored(), Stored());

        plan.Should().BeSameAs(ParsePlan.Skip.Instance);
    }

    [Fact]
    public void Decide_FileGrew_ResumesAtTheStoredOffset()
    {
        var current = Stored() with { Size = StoredSize + 512, MTimeTicks = StoredTicks + 1 };

        var plan = ParsePlanner.Decide(Stored(), current);

        plan.Should().Be(new ParsePlan.Resume(StoredOffset));
    }

    [Fact]
    public void Decide_FileIsShorterThanTheStoredOffset_ReadsItFromTheStart()
    {
        var current = Stored() with { Size = StoredOffset - 1, MTimeTicks = StoredTicks + 1 };

        var plan = ParsePlanner.Decide(Stored(), current);

        plan.Should().Be(new ParsePlan.FullReparse(FullReparseReason.FileTruncated));
    }

    [Fact]
    public void Decide_LeadingBytesChanged_ReadsItFromTheStart()
    {
        var current = Stored() with { Size = StoredSize + 512, MTimeTicks = StoredTicks + 1, HeadSha256 = DifferentHash };

        var plan = ParsePlanner.Decide(Stored(), current);

        plan.Should().Be(new ParsePlan.FullReparse(FullReparseReason.HeadChanged));
    }

    [Fact]
    public void Decide_FileBothShrankAndChanged_ReportsTruncationAsTheReason()
    {
        var current = Stored() with { Size = 10, MTimeTicks = StoredTicks + 1, HeadSha256 = DifferentHash };

        var plan = ParsePlanner.Decide(Stored(), current);

        plan.Should().Be(new ParsePlan.FullReparse(FullReparseReason.FileTruncated));
    }

    [Fact]
    public void Decide_StoredHashMissing_ResumesInsteadOfAssumingARewrite()
    {
        var stored = Stored() with { HeadSha256 = null };
        var current = Stored() with { Size = StoredSize + 512, MTimeTicks = StoredTicks + 1 };

        var plan = ParsePlanner.Decide(stored, current);

        plan.Should().Be(new ParsePlan.Resume(StoredOffset));
    }

    [Fact]
    public void Decide_SizeUnchangedButTimestampMoved_ResumesAtTheStoredOffset()
    {
        var current = Stored() with { MTimeTicks = StoredTicks + 1 };

        var plan = ParsePlanner.Decide(Stored(), current);

        plan.Should().Be(new ParsePlan.Resume(StoredOffset));
    }

    private static FileFingerprint Stored() => new(StoredSize, StoredTicks, StoredHash, StoredOffset);
}
