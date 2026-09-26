using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexSessionFile;
using SessionFinder.Infrastructure.Persistence;
using SessionFinder.Infrastructure.Tests.Persistence;

namespace SessionFinder.Infrastructure.Tests.Indexing;

/// <summary>
/// What a second pass over a transcript that changed does to the index.
/// </summary>
/// <remarks>
/// These exercise the whole incremental path against real files and a real index: the catalogue
/// measures the file, the planner decides from the stored watermark, the parser reads only what it
/// is given, and the writer merges rather than replaces. Every mutation happens to a copy inside a
/// temporary folder.
/// </remarks>
public sealed class IncrementalIndexingTests : IDisposable
{
    private const string ProjectFolder = "C--work-project";
    private const string SessionIdentifier = "5c6d7e8f-9a0b-4c1d-8e2f-3a4b5c6d7e8f";
    private const string SecondSessionIdentifier = "6d7e8f9a-0b1c-4d2e-8f3a-4b5c6d7e8f9a";

    private const string FirstPromptText = "reconcile the kiwi deposits";
    private const string FirstReplyText = "The apricot project builds.";
    private const string SecondPromptText = "now check the papaya transfers";
    private const string SecondReplyText = "The quince settlement is pending.";
    private const string RewrittenPromptText = "start again from the lychee batch";

    private const string GeneratedTitle = "Mango reconciliation sweep";
    private const string ChosenTitle = "Persimmon settlement audit";

    private const string CountChunks = "SELECT count(*) FROM chunks WHERE session_id = $session_id;";
    private const string CountChunksOfKind =
        "SELECT count(*) FROM chunks WHERE session_id = $session_id AND kind = $kind;";
    private const string CountMessageChunksWithText =
        """
        SELECT count(*) FROM chunks
        WHERE session_id = $session_id
          AND text = $text
          AND kind IN ($user_prompt, $assistant_text);
        """;
    private const string CountMatchingChunks = "SELECT count(*) FROM chunks_fts WHERE chunks_fts MATCH $query;";
    private const string CountSessionRows = "SELECT count(*) FROM sessions WHERE session_id = $session_id;";
    private const string SelectTitle = "SELECT title FROM sessions WHERE session_id = $session_id;";
    private const string SelectLastActivity = "SELECT last_activity FROM sessions WHERE session_id = $session_id;";

    private static readonly DateTimeOffset FirstActivity = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterActivity = FirstActivity.AddHours(3);
    private static readonly DateTimeOffset EarlierActivity = FirstActivity.AddHours(-3);

    private readonly FakeTimeProvider _time = new(FirstActivity);
    private readonly IndexingWorkspace _workspace;

    public IncrementalIndexingTests() => _workspace = new IndexingWorkspace(_time);

    [Fact]
    public async Task IndexAsync_MessagesAppendedToAnIndexedTranscript_ReadsOnlyTheAppendedBytes()
    {
        var transcript = await IndexOpeningExchangeAsync();
        var appended = AppendSecondExchange(transcript);

        var result = await _workspace.IndexAsync(transcript);

        result.Outcome.Should().Be(IndexSessionFileOutcome.Appended);
        result.BytesRead.Should().Be(appended);
    }

    [Fact]
    public async Task IndexAsync_MessagesAppendedToAnIndexedTranscript_AdvancesTheOffsetByExactlyTheAppendedBytes()
    {
        var transcript = await IndexOpeningExchangeAsync();
        var offsetBefore = (await RequireIndexedSessionAsync(transcript)).Fingerprint.ParseOffset;
        var appended = AppendSecondExchange(transcript);

        await _workspace.IndexAsync(transcript);

        var stored = await RequireIndexedSessionAsync(transcript);
        stored.Fingerprint.ParseOffset.Should().Be(offsetBefore + appended);
    }

    [Fact]
    public async Task IndexAsync_MessagesAppendedToAnIndexedTranscript_StoresTheNewMessagesAsChunks()
    {
        var transcript = await IndexOpeningExchangeAsync();
        AppendSecondExchange(transcript);

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMessagesWithText(connection, transcript, SecondPromptText).Should().Be(1);
        CountMessagesWithText(connection, transcript, SecondReplyText).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_MessagesAppendedToAnIndexedTranscript_LeavesTheEarlierMessagesExactlyOnce()
    {
        var transcript = await IndexOpeningExchangeAsync();
        AppendSecondExchange(transcript);

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMessagesWithText(connection, transcript, FirstPromptText).Should().Be(1);
        CountMessagesWithText(connection, transcript, FirstReplyText).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_MessagesAppendedToAnIndexedTranscript_KeepsOneTitleAndOneFolderChunk()
    {
        var transcript = await IndexOpeningExchangeAsync();
        AppendSecondExchange(transcript);

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountOfKind(connection, transcript, ChunkKind.Title).Should().Be(1);
        CountOfKind(connection, transcript, ChunkKind.Folder).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_TranscriptIndexedTwiceWithoutChanging_IsSkipped()
    {
        var transcript = await IndexOpeningExchangeAsync();

        var result = await _workspace.IndexAsync(transcript);

        result.Outcome.Should().Be(IndexSessionFileOutcome.Skipped);
    }

    [Fact]
    public async Task IndexAsync_TranscriptRewrittenShorter_ReadsItFromTheStartAgain()
    {
        var transcript = await IndexOpeningExchangeAsync();
        AppendSecondExchange(transcript);
        await _workspace.IndexAsync(transcript);

        transcript.Rewrite(TranscriptLine.RootPrompt(SessionIdentifier, "u9", RewrittenPromptText, FirstActivity));
        var result = await _workspace.IndexAsync(transcript);

        result.Outcome.Should().Be(IndexSessionFileOutcome.Indexed);
    }

    [Fact]
    public async Task IndexAsync_TranscriptRewrittenShorter_LeavesNoChunkFromTheDiscardedText()
    {
        var transcript = await IndexOpeningExchangeAsync();
        AppendSecondExchange(transcript);
        await _workspace.IndexAsync(transcript);

        transcript.Rewrite(TranscriptLine.RootPrompt(SessionIdentifier, "u9", RewrittenPromptText, FirstActivity));
        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMessagesWithText(connection, transcript, FirstPromptText).Should().Be(0);
        CountMessagesWithText(connection, transcript, SecondReplyText).Should().Be(0);
        CountMessagesWithText(connection, transcript, RewrittenPromptText).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_TranscriptRewrittenShorter_StopsTheDiscardedTextFromMatching()
    {
        var transcript = await IndexOpeningExchangeAsync();
        AppendSecondExchange(transcript);
        await _workspace.IndexAsync(transcript);

        transcript.Rewrite(TranscriptLine.RootPrompt(SessionIdentifier, "u9", RewrittenPromptText, FirstActivity));
        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMatching(connection, "apricot").Should().Be(0);
        CountMatching(connection, "lychee").Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IndexAsync_TranscriptEndingMidLine_LeavesTheOffsetBeforeTheUnfinishedLine()
    {
        var transcript = await IndexOpeningExchangeAsync();
        var offsetBefore = (await RequireIndexedSessionAsync(transcript)).Fingerprint.ParseOffset;
        var terminatedBytes = transcript.Append(
            TranscriptLine.Prompt(SessionIdentifier, "u3", "u2", SecondPromptText, FirstActivity));
        transcript.AppendUnterminated(
            TranscriptLine.AssistantText(SessionIdentifier, "a3", "u3", SecondReplyText, FirstActivity));

        await _workspace.IndexAsync(transcript);

        var stored = await RequireIndexedSessionAsync(transcript);
        stored.Fingerprint.ParseOffset.Should().Be(offsetBefore + terminatedBytes);
    }

    [Fact]
    public async Task IndexAsync_TranscriptEndingMidLine_DoesNotIndexTheUnfinishedLine()
    {
        var transcript = await IndexOpeningExchangeAsync();
        transcript.Append(TranscriptLine.Prompt(SessionIdentifier, "u3", "u2", SecondPromptText, FirstActivity));
        transcript.AppendUnterminated(
            TranscriptLine.AssistantText(SessionIdentifier, "a3", "u3", SecondReplyText, FirstActivity));

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMessagesWithText(connection, transcript, SecondReplyText).Should().Be(0);
    }

    [Fact]
    public async Task IndexAsync_TranscriptWhoseUnfinishedLineWasCompleted_IndexesThatMessageExactlyOnce()
    {
        var transcript = await IndexOpeningExchangeAsync();
        transcript.Append(TranscriptLine.Prompt(SessionIdentifier, "u3", "u2", SecondPromptText, FirstActivity));
        transcript.AppendUnterminated(
            TranscriptLine.AssistantText(SessionIdentifier, "a3", "u3", SecondReplyText, FirstActivity));
        await _workspace.IndexAsync(transcript);

        transcript.TerminateLastLine();
        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMessagesWithText(connection, transcript, SecondReplyText).Should().Be(1);
        CountMessagesWithText(connection, transcript, SecondPromptText).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_TranscriptWhoseUnfinishedLineWasCompleted_ReportsNoParseError()
    {
        var transcript = await IndexOpeningExchangeAsync();
        transcript.AppendUnterminated(
            TranscriptLine.AssistantText(SessionIdentifier, "a3", "u2", SecondReplyText, FirstActivity));
        await _workspace.IndexAsync(transcript);

        transcript.TerminateLastLine();
        var result = await _workspace.IndexAsync(transcript);

        result.ParseError.Should().BeNull();
    }

    [Fact]
    public async Task IndexAsync_TitleAppendedAfterTheMessages_ReplacesTheStoredTitle()
    {
        var transcript = await IndexTitledExchangeAsync();
        transcript.Append(TranscriptLine.CustomTitle(SessionIdentifier, ChosenTitle));

        var result = await _workspace.IndexAsync(transcript);

        result.Outcome.Should().Be(IndexSessionFileOutcome.TitleRefreshed);

        using var connection = _workspace.OpenInspector();
        connection.Scalar<string>(SelectTitle, ("$session_id", SessionIdentifier)).Should().Be(ChosenTitle);
    }

    [Fact]
    public async Task IndexAsync_TitleAppendedAfterTheMessages_StopsTheEarlierTitleFromMatching()
    {
        var transcript = await IndexTitledExchangeAsync();

        using (var before = _workspace.OpenInspector())
        {
            CountMatching(before, "mango").Should().Be(1, "the generated title was the searchable title");
        }

        transcript.Append(TranscriptLine.CustomTitle(SessionIdentifier, ChosenTitle));
        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMatching(connection, "mango").Should().Be(0);
        CountMatching(connection, "persimmon").Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_TitleAppendedAfterTheMessages_KeepsExactlyOneTitleChunk()
    {
        var transcript = await IndexTitledExchangeAsync();
        transcript.Append(TranscriptLine.CustomTitle(SessionIdentifier, ChosenTitle));

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountOfKind(connection, transcript, ChunkKind.Title).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_TitleAppendedAfterTheMessages_LeavesTheMessageChunksAlone()
    {
        var transcript = await IndexTitledExchangeAsync();
        transcript.Append(TranscriptLine.CustomTitle(SessionIdentifier, ChosenTitle));

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        CountMessagesWithText(connection, transcript, FirstPromptText).Should().Be(1);
        CountMessagesWithText(connection, transcript, FirstReplyText).Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_ResumedPassWhoseTailIsStampedEarlier_LeavesTheLastActivityWhereItWas()
    {
        var transcript = await IndexExchangeEndingAtAsync(LaterActivity);
        await AppendExchangeStampedAsync(transcript, EarlierActivity);

        using var connection = _workspace.OpenInspector();
        LastActivityOf(connection, transcript).Should().Be(LaterActivity.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task IndexAsync_ResumedPassWhoseTailIsStampedLater_MovesTheLastActivityForward()
    {
        var transcript = await IndexExchangeEndingAtAsync(FirstActivity);
        await AppendExchangeStampedAsync(transcript, LaterActivity);

        using var connection = _workspace.OpenInspector();
        LastActivityOf(connection, transcript).Should().Be(LaterActivity.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task IndexAsync_TranscriptEndingInARecordThatIsNotAMessage_TakesTheLastActivityFromIt()
    {
        var transcript = _workspace.CreateTranscript(ProjectFolder, SessionIdentifier);
        transcript.Append(
            TranscriptLine.RootPrompt(SessionIdentifier, "u1", FirstPromptText, FirstActivity),
            TranscriptLine.AssistantText(SessionIdentifier, "a1", "u1", FirstReplyText, FirstActivity),
            TranscriptLine.SystemNotice(SessionIdentifier, "s1", LaterActivity));

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        LastActivityOf(connection, transcript).Should().Be(LaterActivity.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task IndexAsync_ResumedPassWhoseTailHoldsOnlyARecordThatIsNotAMessage_MovesTheLastActivityForward()
    {
        var transcript = await IndexExchangeEndingAtAsync(FirstActivity);
        transcript.Append(TranscriptLine.SystemNotice(SessionIdentifier, "s1", LaterActivity));

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        LastActivityOf(connection, transcript).Should().Be(LaterActivity.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task IndexAsync_TitleAppendedAfterTheMessages_LeavesTheLastActivityWhereItWas()
    {
        var transcript = await IndexExchangeEndingAtAsync(LaterActivity);
        transcript.Append(TranscriptLine.CustomTitle(SessionIdentifier, ChosenTitle));

        await _workspace.IndexAsync(transcript);

        using var connection = _workspace.OpenInspector();
        LastActivityOf(connection, transcript).Should().Be(LaterActivity.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ReconcileAsync_TranscriptDeleted_RemovesItsSessionRow()
    {
        var (removed, kept) = await IndexTwoSessionsAsync();
        removed.Delete();

        var result = await _workspace.ReconcileAsync();

        result.SessionsPruned.Should().Be(1);

        using var connection = _workspace.OpenInspector();
        CountSessions(connection, removed).Should().Be(0);
        CountSessions(connection, kept).Should().Be(1);
    }

    [Fact]
    public async Task ReconcileAsync_TranscriptDeleted_RemovesEveryChunkItOwned()
    {
        var (removed, kept) = await IndexTwoSessionsAsync();
        removed.Delete();

        await _workspace.ReconcileAsync();

        using var connection = _workspace.OpenInspector();
        CountAll(connection, removed).Should().Be(0);
        CountAll(connection, kept).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReconcileAsync_TranscriptDeleted_StopsItsTextFromMatching()
    {
        var (removed, _) = await IndexTwoSessionsAsync();
        removed.Delete();

        await _workspace.ReconcileAsync();

        using var connection = _workspace.OpenInspector();
        CountMatching(connection, "apricot").Should().Be(0);
        CountMatching(connection, "lychee").Should().BeGreaterThan(0);
    }

    public void Dispose() => _workspace.Dispose();

    /// <summary>
    /// Writes a transcript holding one exchange and indexes it, which is the state every scenario
    /// starts from.
    /// </summary>
    private async Task<LiveTranscript> IndexOpeningExchangeAsync()
    {
        var transcript = _workspace.CreateTranscript(ProjectFolder, SessionIdentifier);
        transcript.Append(
            TranscriptLine.RootPrompt(SessionIdentifier, "u1", FirstPromptText, FirstActivity),
            TranscriptLine.AssistantText(SessionIdentifier, "a1", "u1", FirstReplyText, FirstActivity));

        var result = await _workspace.IndexAsync(transcript);
        result.Outcome.Should().Be(IndexSessionFileOutcome.Indexed);

        return transcript;
    }

    /// <summary>
    /// The same opening exchange, but with a generated title already appended, so a title arriving
    /// later has something to replace that is not also the text of a message.
    /// </summary>
    private async Task<LiveTranscript> IndexTitledExchangeAsync()
    {
        var transcript = _workspace.CreateTranscript(ProjectFolder, SessionIdentifier);
        transcript.Append(
            TranscriptLine.RootPrompt(SessionIdentifier, "u1", FirstPromptText, FirstActivity),
            TranscriptLine.AssistantText(SessionIdentifier, "a1", "u1", FirstReplyText, FirstActivity),
            TranscriptLine.AiTitle(SessionIdentifier, GeneratedTitle));

        var result = await _workspace.IndexAsync(transcript);
        result.Outcome.Should().Be(IndexSessionFileOutcome.Indexed);

        return transcript;
    }

    /// <summary>
    /// Writes and indexes an opening exchange whose final record carries the given moment, so a
    /// later pass has a stored freshness to be compared against rather than one it also produced.
    /// </summary>
    private async Task<LiveTranscript> IndexExchangeEndingAtAsync(DateTimeOffset lastActivity)
    {
        var transcript = _workspace.CreateTranscript(ProjectFolder, SessionIdentifier);
        transcript.Append(
            TranscriptLine.RootPrompt(SessionIdentifier, "u1", FirstPromptText, FirstActivity),
            TranscriptLine.AssistantText(SessionIdentifier, "a1", "u1", FirstReplyText, lastActivity));

        var result = await _workspace.IndexAsync(transcript);
        result.Outcome.Should().Be(IndexSessionFileOutcome.Indexed);

        return transcript;
    }

    /// <summary>
    /// Appends a second exchange stamped with the given moment and indexes it, asserting that the
    /// pass really did resume from the watermark: a full reparse would prove nothing about merging.
    /// </summary>
    private async Task AppendExchangeStampedAsync(LiveTranscript transcript, DateTimeOffset timestamp)
    {
        transcript.Append(
            TranscriptLine.Prompt(SessionIdentifier, "u2", "u1", SecondPromptText, timestamp),
            TranscriptLine.AssistantText(SessionIdentifier, "a2", "u2", SecondReplyText, timestamp));

        var result = await _workspace.IndexAsync(transcript);
        result.Outcome.Should().Be(IndexSessionFileOutcome.Appended);
    }

    private async Task<(LiveTranscript Removed, LiveTranscript Kept)> IndexTwoSessionsAsync()
    {
        var removed = await IndexOpeningExchangeAsync();
        var kept = _workspace.CreateTranscript(ProjectFolder, SecondSessionIdentifier);
        kept.Append(TranscriptLine.RootPrompt(SecondSessionIdentifier, "u1", RewrittenPromptText, FirstActivity));

        await _workspace.ReconcileAsync();

        return (removed, kept);
    }

    private long AppendSecondExchange(LiveTranscript transcript) => transcript.Append(
        TranscriptLine.Prompt(SessionIdentifier, "u2", "u1", SecondPromptText, FirstActivity),
        TranscriptLine.AssistantText(SessionIdentifier, "a2", "u2", SecondReplyText, FirstActivity));

    private async Task<IndexedSession> RequireIndexedSessionAsync(LiveTranscript transcript) =>
        await _workspace.GetIndexedSessionAsync(transcript.SessionId)
        ?? throw new InvalidOperationException($"Session {transcript.SessionId} is not in the index.");

    /// <summary>
    /// Counts the conversation chunks holding exactly this text. Title and folder chunks are left
    /// out on purpose: a session with no title of its own is titled after its first prompt, so
    /// counting every kind would report that prompt twice and say nothing about duplication.
    /// </summary>
    private static long CountMessagesWithText(
        SqliteConnection connection,
        LiveTranscript transcript,
        string text) =>
        connection.Scalar<long>(
            CountMessageChunksWithText,
            ("$session_id", transcript.SessionId.ToString()),
            ("$text", text),
            ("$user_prompt", (int)ChunkKind.UserPrompt),
            ("$assistant_text", (int)ChunkKind.AssistantText));

    private static long CountOfKind(
        SqliteConnection connection,
        LiveTranscript transcript,
        ChunkKind kind) =>
        connection.Scalar<long>(
            CountChunksOfKind,
            ("$session_id", transcript.SessionId.ToString()),
            ("$kind", (int)kind));

    private static long LastActivityOf(SqliteConnection connection, LiveTranscript transcript) =>
        connection.Scalar<long>(SelectLastActivity, ("$session_id", transcript.SessionId.ToString()));

    private static long CountAll(SqliteConnection connection, LiveTranscript transcript) =>
        connection.Scalar<long>(CountChunks, ("$session_id", transcript.SessionId.ToString()));

    private static long CountSessions(SqliteConnection connection, LiveTranscript transcript) =>
        connection.Scalar<long>(CountSessionRows, ("$session_id", transcript.SessionId.ToString()));

    private static long CountMatching(SqliteConnection connection, string userInput) =>
        connection.Scalar<long>(
            CountMatchingChunks,
            ("$query", FtsQueryBuilder.Build(userInput).AnyTermExpression));
}
