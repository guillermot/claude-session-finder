using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Tests.Persistence;

public sealed class SqliteSessionActivityReaderTests : IDisposable
{
    private const string HeadHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

    private static readonly DateTimeOffset Morning = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly TemporaryIndex _index = new();

    [Fact]
    public async Task GetActivityAsync_DatabaseNeverCreated_ReturnsNothing()
    {
        var activity = await _index.Reader.GetActivityAsync(Morning, Morning.AddDays(1), false, CancellationToken.None);

        activity.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActivityAsync_PromptsInsideAndOutsideTheWindow_ReturnsOnlyThoseInside()
    {
        await WriteAsync(
            "a1111111-1111-4111-8111-111111111111",
            new SearchChunk(ChunkKind.UserPrompt, "the day before", Morning.AddDays(-1)),
            new SearchChunk(ChunkKind.UserPrompt, "in the window", Morning.AddHours(1)),
            new SearchChunk(ChunkKind.UserPrompt, "the day after", Morning.AddDays(1)));

        var activity = await _index.Reader.GetActivityAsync(Morning, Morning.AddDays(1), false, CancellationToken.None);

        activity.Should().ContainSingle().Which.Chunks.Select(chunk => chunk.Text).Should().Equal("in the window");
    }

    [Fact]
    public async Task GetActivityAsync_WithoutAssistantText_LeavesRepliesAndTitlesOut()
    {
        await WriteAsync(
            "a2222222-2222-4222-8222-222222222222",
            new SearchChunk(ChunkKind.Title, "a title", Morning.AddHours(1)),
            new SearchChunk(ChunkKind.UserPrompt, "a prompt", Morning.AddHours(1)),
            new SearchChunk(ChunkKind.AssistantText, "a reply", Morning.AddHours(2)));

        var activity = await _index.Reader.GetActivityAsync(Morning, Morning.AddDays(1), false, CancellationToken.None);

        activity.Single().Chunks.Select(chunk => chunk.Kind).Should().Equal(ChunkKind.UserPrompt);
    }

    [Fact]
    public async Task GetActivityAsync_WithAssistantText_ReturnsPromptsAndRepliesInTimeOrder()
    {
        await WriteAsync(
            "a3333333-3333-4333-8333-333333333333",
            new SearchChunk(ChunkKind.AssistantText, "a reply", Morning.AddHours(2)),
            new SearchChunk(ChunkKind.UserPrompt, "a prompt", Morning.AddHours(1)));

        var activity = await _index.Reader.GetActivityAsync(Morning, Morning.AddDays(1), true, CancellationToken.None);

        activity.Single().Chunks.Select(chunk => chunk.Text).Should().Equal("a prompt", "a reply");
    }

    [Fact]
    public async Task GetActivityAsync_TwoSessions_ReturnsEachWithItsOwnFolderAndTitle()
    {
        await WriteAsync("a4444444-4444-4444-8444-444444444444", new SearchChunk(ChunkKind.UserPrompt, "one", Morning.AddHours(1)));
        await WriteAsync("a5555555-5555-4555-8555-555555555555", new SearchChunk(ChunkKind.UserPrompt, "two", Morning.AddHours(2)));

        var activity = await _index.Reader.GetActivityAsync(Morning, Morning.AddDays(1), false, CancellationToken.None);

        activity.Should().HaveCount(2);
        activity.Should().OnlyContain(session => session.Folder.Display == "/work/finder" && session.GitBranch == "main");
        activity.Select(session => session.Title.Text).Should().OnlyContain(title => title.StartsWith("Session "));
    }

    public void Dispose() => _index.Dispose();

    private async Task WriteAsync(string sessionId, params SearchChunk[] chunks)
    {
        var timestamps = chunks.Select(chunk => chunk.Timestamp!.Value).ToList();

        await _index.Writer.WriteAsync(
            new SessionIndexEntry
            {
                Document = new SessionDocument
                {
                    SessionId = new SessionId(Guid.Parse(sessionId)),
                    Title = new SessionTitle($"Session {sessionId[..2]}", TitleSource.AiTitle),
                    TitleCandidates = SessionTitleCandidates.ForFile($"{sessionId}.jsonl"),
                    Folder = WorkingFolder.FromTranscriptCwd("/work/finder"),
                    GitBranch = "main",
                    FirstActivity = timestamps.Min(),
                    LastActivity = timestamps.Max(),
                    ParseOffset = 100,
                    Chunks = chunks,
                },
                FilePath = $"/projects/finder/{sessionId}.jsonl",
                Fingerprint = new FileFingerprint(100, 638_000_000_000_000_000, HeadHash, 100),
                IndexedAt = Morning,
            },
            CancellationToken.None);

        await _index.CheckpointAsync();
    }
}
