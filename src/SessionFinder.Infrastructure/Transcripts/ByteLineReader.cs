using System.Buffers;

namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// Splits a stream into lines on <c>0x0A</c> while counting bytes itself.
/// </summary>
/// <remarks>
/// <para>
/// The byte counting is the whole point. <see cref="StreamReader"/> reads ahead into its own
/// buffer, so <c>BaseStream.Position</c> reports where the reader's buffering got to rather than
/// where the last consumed line ended, which makes it useless as a watermark for incremental
/// parsing.
/// </para>
/// <para>
/// <see cref="CommittedOffset"/> therefore advances only past a newline. A final line that the
/// writer has not finished yet leaves the offset in front of it, so the next pass reads it again.
/// </para>
/// </remarks>
public sealed class ByteLineReader : IDisposable
{
    private const byte LineFeed = 0x0A;
    private const byte CarriageReturn = 0x0D;
    private const int DefaultBufferSize = 64 * 1024;
    private const int ByteOrderMarkLength = 3;

    private readonly Stream _stream;

    private byte[] _buffer;
    private int _dataStart;
    private int _dataLength;
    private int _lineStart;
    private int _lineLength;
    private bool _endOfStream;
    private bool _byteOrderMarkHandled;
    private bool _disposed;

    /// <summary>
    /// Creates a reader over a stream that is already positioned at <paramref name="startOffset"/>.
    /// </summary>
    /// <param name="stream">The stream to read to the end. It is never seeked and never disposed.</param>
    /// <param name="startOffset">
    /// The absolute offset the stream sits at, so <see cref="CommittedOffset"/> is absolute too.
    /// </param>
    public ByteLineReader(Stream stream, long startOffset)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);

        _stream = stream;
        _buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        CommittedOffset = startOffset;
        _byteOrderMarkHandled = startOffset > 0;
    }

    /// <summary>
    /// The absolute offset immediately after the last newline consumed. Safe to persist as a
    /// resume point at any moment.
    /// </summary>
    public long CommittedOffset { get; private set; }

    /// <summary>
    /// Whether the line returned by the last read ended with a newline. A <see langword="false"/>
    /// here means the line is still being written and must not be trusted.
    /// </summary>
    public bool IsCurrentLineTerminated { get; private set; }

    /// <summary>
    /// The bytes of the last line read, without its newline or carriage return. Valid only until
    /// the next call to <see cref="ReadLineAsync"/>.
    /// </summary>
    public ReadOnlySpan<byte> CurrentLine => _buffer.AsSpan(_lineStart, _lineLength);

    /// <summary>
    /// Advances to the next line, growing the buffer as far as the longest line requires.
    /// </summary>
    /// <param name="cancellationToken">Cancels the underlying read.</param>
    /// <returns><see langword="false"/> once the stream is exhausted.</returns>
    public async ValueTask<bool> ReadLineAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (true)
        {
            SkipByteOrderMark();

            var newlineIndex = _buffer.AsSpan(_dataStart, _dataLength).IndexOf(LineFeed);

            if (newlineIndex >= 0)
            {
                TakeTerminatedLine(newlineIndex);
                return true;
            }

            if (_endOfStream)
            {
                return TakeTrailingLine();
            }

            await FillAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Returns the pooled buffer. The underlying stream is left alone.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lineStart = 0;
        _lineLength = 0;
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
    }

    private void TakeTerminatedLine(int newlineIndex)
    {
        var contentLength = newlineIndex;

        if (contentLength > 0 && _buffer[_dataStart + contentLength - 1] == CarriageReturn)
        {
            contentLength--;
        }

        _lineStart = _dataStart;
        _lineLength = contentLength;
        IsCurrentLineTerminated = true;

        var consumed = newlineIndex + 1;
        _dataStart += consumed;
        _dataLength -= consumed;
        CommittedOffset += consumed;
    }

    /// <summary>
    /// Hands back the bytes after the last newline. They are reported as unterminated and do not
    /// move <see cref="CommittedOffset"/>.
    /// </summary>
    private bool TakeTrailingLine()
    {
        if (_dataLength == 0)
        {
            _lineLength = 0;
            IsCurrentLineTerminated = false;
            return false;
        }

        _lineStart = _dataStart;
        _lineLength = _dataLength;
        IsCurrentLineTerminated = false;

        _dataStart += _dataLength;
        _dataLength = 0;

        return true;
    }

    /// <summary>
    /// Drops a leading UTF-8 byte order mark, which <c>Utf8JsonReader</c> rejects as an unexpected
    /// character, while still counting its bytes towards the offset.
    /// </summary>
    private void SkipByteOrderMark()
    {
        if (_byteOrderMarkHandled || (_dataLength < ByteOrderMarkLength && !_endOfStream))
        {
            return;
        }

        _byteOrderMarkHandled = true;

        if (!_buffer.AsSpan(_dataStart, _dataLength).StartsWith(Utf8ByteOrderMark))
        {
            return;
        }

        _dataStart += ByteOrderMarkLength;
        _dataLength -= ByteOrderMarkLength;
        CommittedOffset += ByteOrderMarkLength;
    }

    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        CompactOrGrow();

        var read = await _stream
            .ReadAsync(_buffer.AsMemory(_dataStart + _dataLength, _buffer.Length - _dataStart - _dataLength), cancellationToken)
            .ConfigureAwait(false);

        if (read == 0)
        {
            _endOfStream = true;
            return;
        }

        _dataLength += read;
    }

    /// <summary>
    /// Makes room for another read: first by sliding the live bytes to the front, and only when a
    /// single line already fills the whole buffer by renting a larger one.
    /// </summary>
    private void CompactOrGrow()
    {
        if (_dataStart > 0)
        {
            Array.Copy(_buffer, _dataStart, _buffer, 0, _dataLength);
            _dataStart = 0;
        }

        if (_dataLength < _buffer.Length)
        {
            return;
        }

        var grown = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
        Array.Copy(_buffer, grown, _dataLength);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = grown;
    }

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];
}
