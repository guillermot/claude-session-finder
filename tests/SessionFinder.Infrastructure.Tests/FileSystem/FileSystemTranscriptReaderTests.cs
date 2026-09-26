using System.Text;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Tests.FileSystem;

public sealed class FileSystemTranscriptReaderTests : IDisposable
{
    private const string TranscriptContent = "{\"type\":\"user\"}\n{\"type\":\"assistant\"}\n";
    private const long StartOfFile = 0;

    private readonly string _directory;
    private readonly FileSystemTranscriptReader _reader = new();

    public FileSystemTranscriptReaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "session-finder-reader", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task OpenAsync_Transcript_ReportsItsLength()
    {
        var path = WriteTranscript(TranscriptContent);

        await using var transcript = await _reader.OpenAsync(path, StartOfFile, CancellationToken.None);

        transcript.Length.Should().Be(Encoding.UTF8.GetByteCount(TranscriptContent));
    }

    [Fact]
    public async Task OpenAsync_Transcript_HashesItsLeadingBytes()
    {
        var path = WriteTranscript(TranscriptContent);
        var expected = FileFingerprint.ComputeHeadHash(Encoding.UTF8.GetBytes(TranscriptContent));

        await using var transcript = await _reader.OpenAsync(path, StartOfFile, CancellationToken.None);

        transcript.HeadSha256.Should().Be(expected);
    }

    [Fact]
    public async Task OpenAsync_TranscriptThatWasRewrittenToTheSameLength_HashesDifferently()
    {
        var first = WriteTranscript(TranscriptContent);
        var rewritten = WriteTranscript(TranscriptContent.Replace('u', 'U'), "other.jsonl");

        await using var original = await _reader.OpenAsync(first, StartOfFile, CancellationToken.None);
        await using var changed = await _reader.OpenAsync(rewritten, StartOfFile, CancellationToken.None);

        changed.HeadSha256.Should().NotBe(original.HeadSha256);
    }

    [Fact]
    public async Task OpenAsync_EmptyTranscript_ReportsNoHeadHash()
    {
        var path = WriteTranscript(string.Empty);

        await using var transcript = await _reader.OpenAsync(path, StartOfFile, CancellationToken.None);

        transcript.HeadSha256.Should().BeNull();
    }

    [Fact]
    public async Task OpenAsync_StartOffset_PositionsTheStreamThereRatherThanAfterTheHeadRead()
    {
        var path = WriteTranscript(TranscriptContent);
        const int Offset = 16;

        await using var transcript = await _reader.OpenAsync(path, Offset, CancellationToken.None);

        transcript.Content.Position.Should().Be(Offset);
    }

    [Fact]
    public async Task OpenAsync_TranscriptHeldOpenForWritingByAnotherProcess_StillOpens()
    {
        var path = WriteTranscript(TranscriptContent);
        using var writerHandle = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        await using var transcript = await _reader.OpenAsync(path, StartOfFile, CancellationToken.None);

        transcript.Length.Should().BePositive();
    }

    [Fact]
    public async Task OpenAsync_Transcript_ReportsTheWatermarkAPassWouldStore()
    {
        var path = WriteTranscript(TranscriptContent);
        var writtenAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, writtenAt);

        await using var transcript = await _reader.OpenAsync(path, StartOfFile, CancellationToken.None);

        transcript.ToFingerprint(parseOffset: 12).Should().Be(new FileFingerprint(
            transcript.Length,
            writtenAt.Ticks,
            transcript.HeadSha256,
            12));
    }

    [Fact]
    public async Task OpenAsync_TranscriptThatDoesNotExist_Throws()
    {
        var missing = Path.Combine(_directory, "missing.jsonl");

        var open = async () => await _reader.OpenAsync(missing, StartOfFile, CancellationToken.None);

        await open.Should().ThrowAsync<IOException>();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
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

    private string WriteTranscript(string content, string fileName = "transcript.jsonl")
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, content);

        return path;
    }
}
