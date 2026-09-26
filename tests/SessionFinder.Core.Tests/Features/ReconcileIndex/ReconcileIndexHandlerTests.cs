using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexSessionFile;
using SessionFinder.Core.Features.ReconcileIndex;

namespace SessionFinder.Core.Tests.Features.ReconcileIndex;

public sealed class ReconcileIndexHandlerTests
{
    private const string TranscriptPath = @"C:\projects\encoded-folder\a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001.jsonl";
    private const string TranscriptFileName = "a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001.jsonl";
    private const long TranscriptLength = 4_096;
    private const long MTimeTicks = 638_000_000_000_000_000;
    private const string HeadHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

    private readonly ISessionFileCatalog _catalog = Substitute.For<ISessionFileCatalog>();
    private readonly ITranscriptFileReader _transcripts = Substitute.For<ITranscriptFileReader>();
    private readonly ISessionTranscriptParser _parser = Substitute.For<ISessionTranscriptParser>();
    private readonly ISessionIndexWriter _writer = Substitute.For<ISessionIndexWriter>();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task HandleAsync_TranscriptNeverIndexed_WritesTheParsedSession()
    {
        GivenCatalogReturnsOneTranscript();
        GivenTranscriptOpens();
        GivenParserReturns(BuildDocument());

        var result = await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        result.SessionsIndexed.Should().Be(1);
        await _writer.Received(1).WriteAsync(Arg.Any<SessionIndexEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TranscriptNeverIndexed_StoresTheFingerprintTheNextPassNeeds()
    {
        GivenCatalogReturnsOneTranscript();
        GivenTranscriptOpens();
        GivenParserReturns(BuildDocument(parseOffset: 4_000));

        await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        await _writer.Received(1).WriteAsync(
            Arg.Is<SessionIndexEntry>(entry =>
                entry.Fingerprint == new FileFingerprint(TranscriptLength, MTimeTicks, HeadHash, 4_000)
                && entry.FilePath == TranscriptPath),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_StoredFingerprintMatchesTheFileOnDisk_SkipsWithoutOpeningIt()
    {
        GivenCatalogReturnsOneTranscript();
        GivenStoredSession(new FileFingerprint(TranscriptLength, MTimeTicks, HeadHash, TranscriptLength));

        var result = await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        result.SessionsSkipped.Should().Be(1);
        await _transcripts.DidNotReceive().OpenAsync(
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ForceFullReparse_ReadsEvenAnUnchangedTranscript()
    {
        GivenCatalogReturnsOneTranscript();
        GivenStoredSession(new FileFingerprint(TranscriptLength, MTimeTicks, HeadHash, TranscriptLength));
        GivenTranscriptOpens();
        GivenParserReturns(BuildDocument());

        var result = await CreateHandler().HandleAsync(
            new ReconcileIndexCommand { ForceFullReparse = true },
            CancellationToken.None);

        result.SessionsIndexed.Should().Be(1);
        result.SessionsSkipped.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_TranscriptCannotBeOpened_CountsTheFailureInsteadOfThrowing()
    {
        GivenCatalogReturnsOneTranscript();
        _transcripts
            .OpenAsync(TranscriptPath, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("the file is gone"));

        var result = await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        result.FilesFailed.Should().Be(1);
        result.SessionsIndexed.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_OneTranscriptFails_TheRestOfTheBatchIsStillIndexed()
    {
        var failing = BuildSessionFile("a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001");
        var healthy = BuildSessionFile("b31c9d72-2a60-4c1b-8b4f-8b3efdf1e002");
        _catalog.ListSessionFilesAsync(Arg.Any<CancellationToken>()).Returns([failing, healthy]);
        _transcripts
            .OpenAsync(failing.FilePath, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("locked"));
        _transcripts
            .OpenAsync(healthy.FilePath, Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(BuildOpenTranscript());
        GivenParserReturns(BuildDocument(healthy.SessionId));

        var result = await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        result.FilesFailed.Should().Be(1);
        result.SessionsIndexed.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ParserReportsDamagedLines_StillIndexesTheSessionAndCountsTheError()
    {
        GivenCatalogReturnsOneTranscript();
        GivenTranscriptOpens();
        GivenParserReturns(BuildDocument() with { ParseError = "3 line(s) skipped: not well-formed JSON." });

        var result = await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        result.SessionsIndexed.Should().Be(1);
        result.SessionsWithParseErrors.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Always_PrunesTheSessionsThatAreNoLongerOnDisk()
    {
        var file = BuildSessionFile("a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001");
        _catalog.ListSessionFilesAsync(Arg.Any<CancellationToken>()).Returns([file]);
        GivenStoredSession(new FileFingerprint(TranscriptLength, MTimeTicks, HeadHash, TranscriptLength));
        _writer
            .PruneMissingAsync(Arg.Any<IReadOnlyCollection<SessionId>>(), Arg.Any<CancellationToken>())
            .Returns(4);

        var result = await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        result.SessionsPruned.Should().Be(4);
        await _writer.Received(1).PruneMissingAsync(
            Arg.Is<IReadOnlyCollection<SessionId>>(live => live.Single() == file.SessionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SessionsWereRewritten_CompactsTheIndex()
    {
        GivenCatalogReturnsOneTranscript();
        GivenTranscriptOpens();
        GivenParserReturns(BuildDocument());

        await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        await _writer.Received(1).CompactAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EveryTranscriptSkipped_LeavesTheIndexAlone()
    {
        GivenCatalogReturnsOneTranscript();
        GivenStoredSession(new FileFingerprint(TranscriptLength, MTimeTicks, HeadHash, TranscriptLength));

        await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        await _writer.DidNotReceive().CompactAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassCompletes_RecordsWhenItFinished()
    {
        _catalog.ListSessionFilesAsync(Arg.Any<CancellationToken>()).Returns([]);

        await CreateHandler().HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

        await _writer.Received(1).RecordReconcileAsync(
            _timeProvider.GetUtcNow(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProgressRequested_ReportsOnceForEachTranscript()
    {
        var first = BuildSessionFile("a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001");
        var second = BuildSessionFile("b31c9d72-2a60-4c1b-8b4f-8b3efdf1e002");
        _catalog.ListSessionFilesAsync(Arg.Any<CancellationToken>()).Returns([first, second]);
        GivenTranscriptOpens();
        GivenParserReturns(BuildDocument());
        var reports = new List<ReconcileIndexProgress>();

        await CreateHandler().HandleAsync(
            new ReconcileIndexCommand { Progress = new CollectingProgress(reports) },
            CancellationToken.None);

        reports.Select(report => report.Completed).Should().Equal(1, 2);
    }

    /// <summary>
    /// The pass is exercised over the real per-file handler rather than over a stand-in for it, so
    /// these stay tests of what a pass does to the index and not of how it delegates.
    /// </summary>
    private ReconcileIndexHandler CreateHandler() => new(
        _catalog,
        new IndexSessionFileHandler(
            _transcripts,
            _parser,
            _writer,
            _timeProvider,
            NullLogger<IndexSessionFileHandler>.Instance),
        _writer,
        _timeProvider,
        NullLogger<ReconcileIndexHandler>.Instance);

    private void GivenCatalogReturnsOneTranscript() =>
        _catalog
            .ListSessionFilesAsync(Arg.Any<CancellationToken>())
            .Returns([BuildSessionFile("a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001")]);

    private void GivenTranscriptOpens() =>
        _transcripts
            .OpenAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(_ => BuildOpenTranscript());

    private void GivenParserReturns(SessionDocument document) =>
        _parser.ParseAsync(Arg.Any<TranscriptParseRequest>(), Arg.Any<CancellationToken>()).Returns(document);

    private void GivenStoredSession(FileFingerprint fingerprint) =>
        _writer
            .GetIndexedSessionAsync(Arg.Any<SessionId>(), Arg.Any<CancellationToken>())
            .Returns(new IndexedSession
            {
                SessionId = BuildSessionId("a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001"),
                FilePath = TranscriptPath,
                Fingerprint = fingerprint,
                Title = new SessionTitle("a stored title", TitleSource.CustomTitle),
                TitleCandidates = SessionTitleCandidates.ForFile(TranscriptFileName),
                Folder = WorkingFolder.FromTranscriptCwd(@"C:\git\example"),
            });

    private static OpenTranscript BuildOpenTranscript() => new(
        new MemoryStream(new byte[TranscriptLength]),
        TranscriptLength,
        new DateTimeOffset(MTimeTicks, TimeSpan.Zero),
        HeadHash);

    private static SessionFile BuildSessionFile(string sessionId) => new()
    {
        SessionId = BuildSessionId(sessionId),
        FilePath = $@"C:\projects\encoded-folder\{sessionId}.jsonl",
        FileName = $"{sessionId}.jsonl",
        SizeBytes = TranscriptLength,
        LastWriteTimeUtc = new DateTimeOffset(MTimeTicks, TimeSpan.Zero),
    };

    private static SessionDocument BuildDocument(long parseOffset = TranscriptLength) =>
        BuildDocument(BuildSessionId("a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001"), parseOffset);

    private static SessionDocument BuildDocument(SessionId sessionId, long parseOffset = TranscriptLength) => new()
    {
        SessionId = sessionId,
        Title = new SessionTitle("a parsed title", TitleSource.FirstPrompt),
        TitleCandidates = SessionTitleCandidates.ForFile(TranscriptFileName),
        Folder = WorkingFolder.FromTranscriptCwd(@"C:\git\example"),
        ParseOffset = parseOffset,
        Chunks = [new SearchChunk(ChunkKind.Title, "a parsed title", null)],
    };

    private static SessionId BuildSessionId(string value) => new(Guid.Parse(value));

    private sealed class CollectingProgress(List<ReconcileIndexProgress> reports) : IProgress<ReconcileIndexProgress>
    {
        public void Report(ReconcileIndexProgress value) => reports.Add(value);
    }
}
