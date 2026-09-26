using System.Text;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Infrastructure.Tests.Transcripts;

/// <summary>
/// Covers the byte accounting that the incremental indexer depends on, including the case that
/// makes it necessary at all: a final line the writer has not finished yet.
/// </summary>
public sealed class ByteLineReaderTests
{
    private const string FirstLine = "{\"n\":1}";
    private const string SecondLine = "{\"n\":2}";

    [Fact]
    public async Task ReadLineAsync_EmptyStream_ReportsNothingToRead()
    {
        await using var stream = new MemoryStream([]);
        using var reader = new ByteLineReader(stream, 0);

        var read = await reader.ReadLineAsync(CancellationToken.None);

        read.Should().BeFalse();
    }

    [Fact]
    public async Task ReadLineAsync_EveryLineTerminated_StopsAtTheLengthOfTheStream()
    {
        var bytes = Encoding.UTF8.GetBytes($"{FirstLine}\n{SecondLine}\n");

        var offset = await ReadToEndAsync(bytes);

        offset.Should().Be(bytes.Length);
    }

    [Fact]
    public async Task ReadLineAsync_LastLineNotTerminated_LeavesTheOffsetInFrontOfIt()
    {
        var bytes = Encoding.UTF8.GetBytes($"{FirstLine}\n{SecondLine}");

        var offset = await ReadToEndAsync(bytes);

        offset.Should().Be(FirstLine.Length + 1);
    }

    [Fact]
    public async Task ReadLineAsync_LastLineNotTerminated_ReportsItAsUnterminated()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{FirstLine}\n{SecondLine}"));
        using var reader = new ByteLineReader(stream, 0);

        await reader.ReadLineAsync(CancellationToken.None);
        await reader.ReadLineAsync(CancellationToken.None);

        reader.IsCurrentLineTerminated.Should().BeFalse();
    }

    [Fact]
    public async Task ReadLineAsync_CarriageReturnBeforeTheNewline_IsNotPartOfTheLine()
    {
        var lines = await ReadAllLinesAsync(Encoding.UTF8.GetBytes($"{FirstLine}\r\n{SecondLine}\n"));

        lines.Should().Equal(FirstLine, SecondLine);
    }

    [Fact]
    public async Task ReadLineAsync_MixedLineEndings_CountsBothTerminatorsCorrectly()
    {
        var bytes = Encoding.UTF8.GetBytes($"{FirstLine}\r\n{SecondLine}\n");

        var offset = await ReadToEndAsync(bytes);

        offset.Should().Be(bytes.Length);
    }

    [Fact]
    public async Task ReadLineAsync_ByteOrderMark_IsNotHandedToTheJsonReader()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes($"{FirstLine}\n")).ToArray();

        var lines = await ReadAllLinesAsync(bytes);

        lines.Should().Equal(FirstLine);
    }

    [Fact]
    public async Task ReadLineAsync_ByteOrderMark_IsStillCountedTowardsTheOffset()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes($"{FirstLine}\n")).ToArray();

        var offset = await ReadToEndAsync(bytes);

        offset.Should().Be(bytes.Length);
    }

    [Fact]
    public async Task ReadLineAsync_ByteOrderMarkArrivingOneByteAtATime_IsStillRecognised()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes($"{FirstLine}\n")).ToArray();

        await using var stream = new SingleByteStream(bytes);
        var lines = await ReadAllLinesAsync(stream);

        lines.Should().Equal(FirstLine);
    }

    [Fact]
    public async Task ReadLineAsync_LineLongerThanTheInitialBuffer_IsReturnedWhole()
    {
        var longLine = new string('x', 700 * 1024);
        var bytes = Encoding.UTF8.GetBytes($"{longLine}\n{SecondLine}\n");

        var lines = await ReadAllLinesAsync(bytes);

        lines.Should().Equal(longLine, SecondLine);
    }

    [Fact]
    public async Task ReadLineAsync_EmptyLineBetweenRecords_IsReturnedAsAnEmptyLine()
    {
        var lines = await ReadAllLinesAsync(Encoding.UTF8.GetBytes($"{FirstLine}\n\n{SecondLine}\n"));

        lines.Should().Equal(FirstLine, string.Empty, SecondLine);
    }

    [Fact]
    public async Task ReadLineAsync_StartOffsetGiven_ReportsAbsoluteOffsets()
    {
        var bytes = Encoding.UTF8.GetBytes($"{SecondLine}\n");
        const long startOffset = 1_000;

        await using var stream = new MemoryStream(bytes);
        using var reader = new ByteLineReader(stream, startOffset);
        await reader.ReadLineAsync(CancellationToken.None);

        reader.CommittedOffset.Should().Be(startOffset + bytes.Length);
    }

    [Fact]
    public async Task ReadLineAsync_ResumedAfterAByteOrderMark_DoesNotStripTheFirstBytesAgain()
    {
        var bytes = Encoding.UTF8.GetBytes($"{SecondLine}\n");

        await using var stream = new MemoryStream(bytes);
        using var reader = new ByteLineReader(stream, 100);
        await reader.ReadLineAsync(CancellationToken.None);

        Encoding.UTF8.GetString(reader.CurrentLine).Should().Be(SecondLine);
    }

    private static Task<long> ReadToEndAsync(byte[] bytes) => ReadToEndAsync(new MemoryStream(bytes));

    private static async Task<long> ReadToEndAsync(Stream stream)
    {
        await using (stream)
        {
            using var reader = new ByteLineReader(stream, 0);

            while (await reader.ReadLineAsync(CancellationToken.None))
            {
            }

            return reader.CommittedOffset;
        }
    }

    private static Task<List<string>> ReadAllLinesAsync(byte[] bytes) => ReadAllLinesAsync(new MemoryStream(bytes));

    private static async Task<List<string>> ReadAllLinesAsync(Stream stream)
    {
        var lines = new List<string>();

        await using (stream)
        {
            using var reader = new ByteLineReader(stream, 0);

            while (await reader.ReadLineAsync(CancellationToken.None))
            {
                lines.Add(Encoding.UTF8.GetString(reader.CurrentLine));
            }
        }

        return lines;
    }
}
