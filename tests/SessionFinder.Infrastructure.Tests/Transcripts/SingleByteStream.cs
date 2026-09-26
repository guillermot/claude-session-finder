namespace SessionFinder.Infrastructure.Tests.Transcripts;

/// <summary>
/// A read-only stream that hands back one byte per call, so buffer-boundary handling is exercised
/// instead of being hidden by a single large read from a <see cref="MemoryStream"/>.
/// </summary>
internal sealed class SingleByteStream(byte[] content) : Stream
{
    private readonly byte[] _content = content;
    private int _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => _content.Length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty || _position >= _content.Length)
        {
            return 0;
        }

        buffer[0] = _content[_position++];

        return 1;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
