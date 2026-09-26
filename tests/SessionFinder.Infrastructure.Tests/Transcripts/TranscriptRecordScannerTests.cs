using System.Text;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Infrastructure.Tests.Transcripts;

/// <summary>
/// Covers the field extraction and, just as importantly, the two early-outs: both are observable
/// as fields the scanner deliberately never gets to.
/// </summary>
public sealed class TranscriptRecordScannerTests
{
    private const string WorkingDirectory = @"C:\work\project";

    [Fact]
    public void TryScan_RootUserRecord_ReportsNoParent()
    {
        var record = Scan($$"""
            {"parentUuid":null,"type":"user","cwd":"C:\\work\\project"}
            """);

        record.HasParent.Should().BeFalse();
    }

    [Fact]
    public void TryScan_ReplyRecord_ReportsAParent()
    {
        var record = Scan("""
            {"parentUuid":"u1","type":"user","cwd":"C:\\work\\project"}
            """);

        record.HasParent.Should().BeTrue();
    }

    [Fact]
    public void TryScan_UserRecord_ReadsTheWorkingDirectory()
    {
        var record = Scan("""
            {"parentUuid":null,"type":"user","cwd":"C:\\work\\project","gitBranch":"main"}
            """);

        record.WorkingDirectory.Should().Be(WorkingDirectory);
    }

    [Fact]
    public void TryScan_Timestamp_IsReadAsAnInstant()
    {
        var record = Scan("""
            {"type":"user","timestamp":"2026-03-01T10:00:00.000Z"}
            """);

        record.Timestamp.Should().Be(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("custom-title", "customTitle", TranscriptRecordType.CustomTitle)]
    [InlineData("ai-title", "aiTitle", TranscriptRecordType.AiTitle)]
    [InlineData("last-prompt", "lastPrompt", TranscriptRecordType.LastPrompt)]
    public void TryScan_TitleBearingRecord_ReadsItsPayload(string type, string property, TranscriptRecordType expected)
    {
        var record = Scan($$"""
            {"type":"{{type}}","{{property}}":"the payload"}
            """);

        record.Type.Should().Be(expected);
        record.TitleText.Should().Be("the payload");
    }

    [Fact]
    public void TryScan_IgnoredType_StopsBeforeReadingTheFieldsThatFollowIt()
    {
        var record = Scan("""
            {"parentUuid":null,"type":"attachment","cwd":"C:\\work\\project"}
            """);

        record.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public void TryScan_IgnoredType_StillReadsItsTimestamp()
    {
        var record = Scan("""
            {"type":"system","timestamp":"2026-03-01T10:00:00.000Z","content":"a tool was run"}
            """);

        record.Timestamp.Should().Be(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TryScan_IgnoredType_StopsOnceItHasTheTimestamp()
    {
        var record = Scan("""
            {"type":"system","timestamp":"2026-03-01T10:00:00.000Z","cwd":"C:\\work\\project"}
            """);

        record.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public void TryScan_ToolUseResultAfterTheTimestamp_KeepsTheTimestamp()
    {
        var record = Scan("""
            {"type":"user","timestamp":"2026-03-01T10:00:00.000Z","toolUseResult":{"stdout":"x"}}
            """);

        record.HasToolUseResult.Should().BeTrue();
        record.Timestamp.Should().Be(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TryScan_ToolUseResult_StopsBeforeReadingTheFieldsThatFollowIt()
    {
        var record = Scan("""
            {"type":"user","toolUseResult":{"stdout":"x"},"cwd":"C:\\work\\project"}
            """);

        record.HasToolUseResult.Should().BeTrue();
        record.WorkingDirectory.Should().BeNull();
    }

    [Fact]
    public void TryScan_UnknownType_IsStillScannedForItsWorkingDirectory()
    {
        var record = Scan("""
            {"type":"a-kind-this-build-has-never-seen","cwd":"C:\\work\\project"}
            """);

        record.Type.Should().Be(TranscriptRecordType.Unknown);
        record.WorkingDirectory.Should().Be(WorkingDirectory);
    }

    [Fact]
    public void TryScan_MessageObject_IsLocatedWithoutBeingRead()
    {
        const string line = """
            {"type":"user","message":{"role":"user","content":"hello"},"cwd":"C:\\work\\project"}
            """;

        var record = Scan(line);

        var message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(line), record.MessageStart, record.MessageLength);
        message.Should().Be("""{"role":"user","content":"hello"}""");
    }

    [Fact]
    public void TryScan_MetaRecord_IsFlagged()
    {
        var record = Scan("""
            {"type":"user","isMeta":true,"cwd":"C:\\work\\project"}
            """);

        record.IsMeta.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"type":"user","message":{"role":"user","conte""")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void TryScan_LineThatIsNotACompleteJsonObject_Fails(string line)
    {
        var scanned = TranscriptRecordScanner.TryScan(Encoding.UTF8.GetBytes(line), out _);

        scanned.Should().BeFalse();
    }

    private static TranscriptRecord Scan(string line)
    {
        TranscriptRecordScanner.TryScan(Encoding.UTF8.GetBytes(line), out var record).Should().BeTrue();

        return record;
    }
}
