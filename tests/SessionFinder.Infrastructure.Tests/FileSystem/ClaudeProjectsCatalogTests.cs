using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Tests.FileSystem;

public sealed class ClaudeProjectsCatalogTests : IDisposable
{
    private const string FirstSessionId = "a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001";
    private const string SecondSessionId = "b31c9d72-2a60-4c1b-8b4f-8b3efdf1e002";
    private const string SubagentSessionId = "c42dae83-3b71-4d2c-9c50-9c4f0e02f003";
    private const string ProjectDirectoryName = "C--git-Example";

    private readonly string _configDirectory;
    private readonly string _projectDirectory;

    public ClaudeProjectsCatalogTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), "session-finder-catalog", Guid.NewGuid().ToString("n"));
        _projectDirectory = Path.Combine(
            _configDirectory,
            ClaudeConfigurationDirectory.ProjectsFolderName,
            ProjectDirectoryName);

        Directory.CreateDirectory(_projectDirectory);
    }

    [Fact]
    public async Task ListSessionFilesAsync_TranscriptInAProjectDirectory_IsListed()
    {
        WriteTranscript(FirstSessionId);

        var files = await CreateCatalog().ListSessionFilesAsync(CancellationToken.None);

        files.Should().ContainSingle().Which.FileName.Should().Be($"{FirstSessionId}.jsonl");
    }

    [Fact]
    public async Task ListSessionFilesAsync_SidecarDirectoryHoldingSubagentTranscripts_IsNotDescendedInto()
    {
        WriteTranscript(FirstSessionId);
        WriteSubagentTranscript();

        var files = await CreateCatalog().ListSessionFilesAsync(CancellationToken.None);

        files.Should().ContainSingle();
    }

    [Fact]
    public async Task ListSessionFilesAsync_FileNameThatIsNotASessionIdentifier_IsIgnored()
    {
        WriteTranscript(FirstSessionId);
        File.WriteAllText(Path.Combine(_projectDirectory, "notes.jsonl"), "{}");

        var files = await CreateCatalog().ListSessionFilesAsync(CancellationToken.None);

        files.Should().ContainSingle();
    }

    [Fact]
    public async Task ListSessionFilesAsync_FileThatIsNotATranscript_IsIgnored()
    {
        WriteTranscript(FirstSessionId);
        File.WriteAllText(Path.Combine(_projectDirectory, $"{SecondSessionId}.json"), "{}");

        var files = await CreateCatalog().ListSessionFilesAsync(CancellationToken.None);

        files.Should().ContainSingle();
    }

    [Fact]
    public async Task ListSessionFilesAsync_SeveralTranscripts_OrdersTheMostRecentlyWrittenFirst()
    {
        var older = WriteTranscript(FirstSessionId);
        var newer = WriteTranscript(SecondSessionId);
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var files = await CreateCatalog().ListSessionFilesAsync(CancellationToken.None);

        files.Select(file => file.FileName).Should().Equal($"{SecondSessionId}.jsonl", $"{FirstSessionId}.jsonl");
    }

    [Fact]
    public async Task ListSessionFilesAsync_Transcript_ReportsItsSizeAndLastWriteTime()
    {
        var path = WriteTranscript(FirstSessionId);
        var writtenAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, writtenAt);

        var files = await CreateCatalog().ListSessionFilesAsync(CancellationToken.None);

        files[0].SizeBytes.Should().Be(new FileInfo(path).Length);
        files[0].LastWriteTimeUtc.Should().Be(new DateTimeOffset(writtenAt, TimeSpan.Zero));
    }

    [Fact]
    public async Task ListSessionFilesAsync_ProjectsDirectoryThatDoesNotExist_ReturnsNothing()
    {
        var catalog = new ClaudeProjectsCatalog(Options.Create(new FinderOptions
        {
            ClaudeConfigDirectory = Path.Combine(_configDirectory, "missing"),
        }));

        var files = await catalog.ListSessionFilesAsync(CancellationToken.None);

        files.Should().BeEmpty();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_configDirectory, recursive: true);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }

    private ClaudeProjectsCatalog CreateCatalog() =>
        new(Options.Create(new FinderOptions { ClaudeConfigDirectory = _configDirectory }));

    private string WriteTranscript(string sessionId)
    {
        var path = Path.Combine(_projectDirectory, $"{sessionId}.jsonl");
        File.WriteAllText(path, "{\"type\":\"user\"}\n");

        return path;
    }

    /// <summary>
    /// Recreates the sidecar layout measured on a real machine: a directory named after a session
    /// sits beside the transcripts and holds subagent transcripts, which are not sessions.
    /// </summary>
    private void WriteSubagentTranscript()
    {
        var sidecar = Path.Combine(_projectDirectory, FirstSessionId, "subagents");
        Directory.CreateDirectory(sidecar);
        File.WriteAllText(Path.Combine(sidecar, $"{SubagentSessionId}.jsonl"), "{\"type\":\"user\"}\n");
    }
}
