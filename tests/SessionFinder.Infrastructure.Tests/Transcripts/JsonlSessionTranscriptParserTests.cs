using System.Text;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Tests.Transcripts;

/// <summary>
/// Runs the parser against the versioned fixtures, one hazard per test.
/// </summary>
public sealed class JsonlSessionTranscriptParserTests
{
    private const string SampleFolder = @"C:\Work\Project";
    private const string PayoutsFolder = @"C:\work\payouts";
    private const string ToolResultMarker = "batch-0001";

    [Theory]
    [MemberData(nameof(CompleteFixtures))]
    public async Task ParseAsync_FixtureEndsWithANewline_StopsExactlyAtTheEndOfTheFile(string fixtureName)
    {
        var document = await TranscriptFixtures.ParseAsync(fixtureName);

        document.ParseOffset.Should().Be(TranscriptFixtures.LengthOf(fixtureName));
    }

    [Fact]
    public async Task ParseAsync_ContentIsAPlainString_StillProducesAPrompt()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.ContentAsPlainString);

        TextOf(document, ChunkKind.UserPrompt).Should().Contain("check the invoice importer for duplicated rows");
    }

    [Fact]
    public async Task ParseAsync_ContentIsAnArray_ProducesAPromptToo()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.ContentAsPlainString);

        TextOf(document, ChunkKind.UserPrompt).Should().Contain("also review the retry policy");
    }

    [Fact]
    public async Task ParseAsync_UserRecordCarriesAToolResult_IsNotIndexedAsAPrompt()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.UserRecordWithToolResult);

        document.Chunks.Should().NotContain(chunk => chunk.Text.Contains(ToolResultMarker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_UserRecordCarriesAToolResult_LeavesTheOtherPromptIntact()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.UserRecordWithToolResult);

        TextOf(document, ChunkKind.UserPrompt).Should().ContainSingle().Which.Should().Be("list the failed batches");
    }

    [Fact]
    public async Task ParseAsync_AssistantToolUseBlocks_AreNotIndexed()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.UserRecordWithToolResult);

        TextOf(document, ChunkKind.AssistantText).Should().ContainSingle()
            .Which.Should().Be("Three batches failed overnight.");
    }

    [Fact]
    public async Task ParseAsync_IgnoredRecordTypes_ContributeNoText()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.IgnoredRecordTypes);

        document.Chunks.Should().NotContain(chunk => chunk.Text.Contains("IGNORED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_IgnoredRecordTypes_DoNotSupplyTheWorkingFolder()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.IgnoredRecordTypes);

        document.Folder.Display.Should().Be(@"C:\work\project");
    }

    [Fact]
    public async Task ParseAsync_TitleRecordsAppendedAfterTheMessages_WinOverTheFirstPrompt()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.TitlesAppendedAfterMessages);

        document.Title.Should().Be(new SessionTitle("Settlement mismatch, week 9", TitleSource.CustomTitle));
    }

    [Fact]
    public async Task ParseAsync_LastPromptRecord_IsIndexedOnItsOwn()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.TitlesAppendedAfterMessages);

        TextOf(document, ChunkKind.LastPrompt).Should().ContainSingle()
            .Which.Should().Be("trace the settlement mismatch");
    }

    [Fact]
    public async Task ParseAsync_CwdCasingDriftsAcrossTheSession_TakesTheFolderOfTheRootRecord()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.MixedCwdCasing);

        document.Folder.Display.Should().Be(SampleFolder);
    }

    [Fact]
    public async Task ParseAsync_CwdCasingDriftsAcrossTheSession_ProducesOneGroupingKey()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.MixedCwdCasing);

        document.Folder.Key.Should().Be(WorkingFolder.NormalizeKey(SampleFolder));
    }

    [Fact]
    public async Task ParseAsync_LineLargerThanTheReadBuffer_DoesNotLoseTheFollowingRecords()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.SingleHugeLine);

        TextOf(document, ChunkKind.AssistantText).Should().ContainSingle()
            .Which.Should().Be("The log is long but consistent.");
    }

    [Fact]
    public async Task ParseAsync_LastLineIsTruncated_LeavesTheOffsetInFrontOfIt()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.TruncatedLastLine);

        document.ParseOffset.Should().BeLessThan(TranscriptFixtures.LengthOf(TranscriptFixtures.TruncatedLastLine));
    }

    [Fact]
    public async Task ParseAsync_LastLineIsTruncated_IsNotReportedAsAParseError()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.TruncatedLastLine);

        document.ParseError.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_LastLineIsTruncated_DoesNotIndexIt()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.TruncatedLastLine);

        document.Chunks.Should().NotContain(chunk => chunk.Text.Contains("never finished", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_AccentedText_SurvivesUnchanged()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.SpanishAccents);

        document.Title.Text.Should().Be("Depósito pendiente de acreditación");
    }

    [Fact]
    public async Task ParseAsync_RootRecordArrivesAfterASystemRecord_TakesTheUserFolder()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.RootUserAfterSystem);

        document.Folder.Display.Should().Be(PayoutsFolder);
    }

    [Fact]
    public async Task ParseAsync_RootRecordArrivesAfterASystemRecord_TakesItsBranchToo()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.RootUserAfterSystem);

        document.GitBranch.Should().Be("release/9");
    }

    [Fact]
    public async Task ParseAsync_MixedLineEndingsAndAByteOrderMark_ReadsEveryRecord()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.CrlfAndByteOrderMark);

        TextOf(document, ChunkKind.UserPrompt).Should().Equal("audit the fee table", "and the rounding rule");
    }

    [Fact]
    public async Task ParseAsync_KnownFolder_IsIndexedSoTheFolderNameIsSearchable()
    {
        var document = await TranscriptFixtures.ParseAsync(TranscriptFixtures.RootUserAfterSystem);

        TextOf(document, ChunkKind.Folder).Should().ContainSingle()
            .Which.Should().Be($"{PayoutsFolder} release/9");
    }

    [Fact]
    public async Task ParseAsync_CompleteLineThatIsNotJson_IsCountedAsAParseError()
    {
        var document = await ParseBytesAsync(BuildTranscriptWithADamagedMiddleLine());

        document.ParseError.Should().NotBeNull();
    }

    [Fact]
    public async Task ParseAsync_CompleteLineThatIsNotJson_DoesNotStopTheFollowingRecords()
    {
        var document = await ParseBytesAsync(BuildTranscriptWithADamagedMiddleLine());

        TextOf(document, ChunkKind.UserPrompt).Should().Contain("the record after the damaged one");
    }

    [Fact]
    public async Task ParseAsync_ResumedFromTheStoredOffset_PicksUpALateTitle()
    {
        var (_, second) = await ParseInTwoPassesAsync();

        second.Title.Should().Be(new SessionTitle("Settlement mismatch, week 9", TitleSource.CustomTitle));
    }

    [Fact]
    public async Task ParseAsync_ResumedFromTheStoredOffset_KeepsTheFolderTheFirstPassFound()
    {
        var (first, second) = await ParseInTwoPassesAsync();

        second.Folder.Should().Be(first.Folder);
    }

    [Fact]
    public async Task ParseAsync_ResumedFromTheStoredOffset_EndsAtTheSameOffsetAsAFullPass()
    {
        var (_, second) = await ParseInTwoPassesAsync();

        second.ParseOffset.Should().Be(TranscriptFixtures.LengthOf(TranscriptFixtures.TitlesAppendedAfterMessages));
    }

    /// <summary>Fixtures whose last line is newline-terminated, for the offset invariant.</summary>
    public static TheoryData<string> CompleteFixtures => TranscriptFixtures.Complete;

    /// <summary>
    /// Runs a first pass over the messages only, then a second over the title records that a live
    /// session appends afterwards, exactly as the incremental indexer will.
    /// </summary>
    private static async Task<(SessionDocument First, SessionDocument Second)> ParseInTwoPassesAsync()
    {
        var bytes = TranscriptFixtures.BytesOf(TranscriptFixtures.TitlesAppendedAfterMessages);
        var split = OffsetAfterLine(bytes, 2);

        await using var head = new MemoryStream(bytes, 0, split, writable: false);
        var first = await TranscriptFixtures.ParseAsync(
            TranscriptFixtures.BuildRequest(TranscriptFixtures.TitlesAppendedAfterMessages, head),
            CancellationToken.None);

        await using var tail = new MemoryStream(bytes, split, bytes.Length - split, writable: false);
        var second = await TranscriptFixtures.ParseAsync(
            new TranscriptParseRequest
            {
                SessionId = TranscriptFixtures.ParseIdentifier(),
                FileName = TranscriptFixtures.TitlesAppendedAfterMessages,
                Content = tail,
                StartOffset = first.ParseOffset,
                KnownTitles = first.TitleCandidates,
                KnownFolder = first.Folder,
            },
            CancellationToken.None);

        return (first, second);
    }

    private static int OffsetAfterLine(byte[] bytes, int lineCount)
    {
        var offset = 0;

        for (var line = 0; line < lineCount; line++)
        {
            offset = Array.IndexOf(bytes, (byte)'\n', offset) + 1;
        }

        return offset;
    }

    private static async Task<SessionDocument> ParseBytesAsync(byte[] transcript)
    {
        await using var stream = new MemoryStream(transcript, writable: false);

        return await TranscriptFixtures.ParseAsync(
            TranscriptFixtures.BuildRequest("damaged.jsonl", stream),
            CancellationToken.None);
    }

    private static byte[] BuildTranscriptWithADamagedMiddleLine()
    {
        const string damagedTranscript = """
            {"parentUuid":null,"type":"user","message":{"role":"user","content":"the first record"},"cwd":"C:\\work\\project","timestamp":"2026-03-01T10:00:00.000Z"}
            {"parentUuid":null,"type":"user","message":{"role":"user","conte
            {"parentUuid":"u1","type":"user","message":{"role":"user","content":"the record after the damaged one"},"cwd":"C:\\work\\project","timestamp":"2026-03-01T10:01:00.000Z"}

            """;

        return Encoding.UTF8.GetBytes(damagedTranscript.ReplaceLineEndings("\n"));
    }

    private static List<string> TextOf(SessionDocument document, ChunkKind kind) =>
        document.Chunks.Where(chunk => chunk.Kind == kind).Select(chunk => chunk.Text).ToList();
}
