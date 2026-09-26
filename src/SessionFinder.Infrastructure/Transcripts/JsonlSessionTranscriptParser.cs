using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// Reads a Claude Code <c>.jsonl</c> transcript into a <see cref="SessionDocument"/>.
/// </summary>
/// <remarks>
/// <para>
/// The invariant that makes incremental indexing safe: the returned parse offset only ever sits
/// immediately after a newline. Claude appends to these files while the indexer reads them, so the
/// last line is regularly half-written; it is left unread, and the next pass sees it whole.
/// </para>
/// </remarks>
public sealed class JsonlSessionTranscriptParser : ISessionTranscriptParser
{
    private const int CancellationCheckInterval = 256;

    /// <inheritdoc />
    public async Task<SessionDocument> ParseAsync(TranscriptParseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accumulator = new TranscriptAccumulator(request);

        using var reader = new ByteLineReader(request.Content, request.StartOffset);
        var linesRead = 0;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsCurrentLineTerminated)
            {
                break;
            }

            accumulator.Accept(reader.CurrentLine);

            if (++linesRead % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return accumulator.Build(reader.CommittedOffset);
    }
}
