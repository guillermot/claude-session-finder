using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.IndexSessionFile;

/// <summary>
/// Indexes one transcript, executing whichever plan the watermark calls for.
/// </summary>
/// <remarks>
/// <para>
/// This is where the incremental promise is kept or broken. A resumed pass opens the file at the
/// stored offset and reads only what was appended, which is what keeps an active session cheap:
/// the alternative, reading a six megabyte transcript again because one line arrived, is the
/// difference between a watcher that can run while you work and one that cannot.
/// </para>
/// <para>
/// A resumed pass starts after the record that carries the working directory and before the title
/// records that Claude appends when a session is renamed. It is handed what the index already
/// knows for exactly that reason: without it the folder would be reported as unknown and the title
/// would fall back to the file name.
/// </para>
/// <para>
/// The failure policy is deliberate: an unreadable transcript is logged, counted and left behind,
/// and the session keeps whatever the index already had. Neither an exception that escapes nor a
/// silent loss.
/// </para>
/// </remarks>
public sealed class IndexSessionFileHandler(
    ITranscriptFileReader transcripts,
    ISessionTranscriptParser parser,
    ISessionIndexWriter writer,
    TimeProvider timeProvider,
    ILogger<IndexSessionFileHandler> logger) : IIndexSessionFileHandler
{
    private const long StartOfFile = 0;

    /// <inheritdoc />
    public async Task<IndexSessionFileResult> HandleAsync(
        IndexSessionFileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var file = command.File;
        var stored = await writer.GetIndexedSessionAsync(file.SessionId, cancellationToken).ConfigureAwait(false);
        var plan = DecidePlan(command, stored);

        try
        {
            return await ExecuteAsync(plan, file, stored, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return Missing(file);
        }
        catch (DirectoryNotFoundException)
        {
            return Missing(file);
        }
        catch (IOException exception)
        {
            return Unreadable(file, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unreadable(file, exception);
        }
    }

    /// <summary>
    /// Chooses the plan. A forced pass never resumes: the point of asking for one is to distrust
    /// the watermark, and resuming from it would trust it.
    /// </summary>
    private static ParsePlan DecidePlan(IndexSessionFileCommand command, IndexedSession? stored) =>
        command.ForceFullReparse
            ? new ParsePlan.FullReparse(FullReparseReason.Forced)
            : ParsePlanner.Decide(stored?.Fingerprint, command.File.ToFingerprint());

    private async Task<IndexSessionFileResult> ExecuteAsync(
        ParsePlan plan,
        SessionFile file,
        IndexedSession? stored,
        CancellationToken cancellationToken)
    {
        switch (plan)
        {
            case ParsePlan.Skip:
                return new IndexSessionFileResult
                {
                    SessionId = file.SessionId,
                    Outcome = IndexSessionFileOutcome.Skipped,
                    ParseOffset = stored?.Fingerprint.ParseOffset ?? StartOfFile,
                };

            case ParsePlan.Resume resume when stored is not null:
                IndexSessionFileLog.ResumingTranscript(logger, file.SessionId, resume.Offset, file.SizeBytes);
                return await ResumeAsync(file, stored, resume.Offset, cancellationToken).ConfigureAwait(false);

            case ParsePlan.FullReparse full:
                IndexSessionFileLog.ReadingWholeTranscript(logger, file.SessionId, full.Reason, file.SizeBytes);
                return await ReadWholeAsync(file, cancellationToken).ConfigureAwait(false);

            default:
                return await ReadWholeAsync(file, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IndexSessionFileResult> ReadWholeAsync(SessionFile file, CancellationToken cancellationToken)
    {
        await using var transcript = await transcripts
            .OpenAsync(file.FilePath, StartOfFile, cancellationToken)
            .ConfigureAwait(false);

        var request = new TranscriptParseRequest
        {
            SessionId = file.SessionId,
            FileName = file.FileName,
            Content = transcript.Content,
            StartOffset = StartOfFile,
        };

        var document = await parser.ParseAsync(request, cancellationToken).ConfigureAwait(false);

        await writer
            .WriteAsync(BuildEntry(file, transcript, document), cancellationToken)
            .ConfigureAwait(false);

        IndexSessionFileLog.TranscriptIndexed(
            logger,
            file.SessionId,
            document.Chunks.Count,
            document.ParseOffset,
            transcript.Length);

        return Describe(file, document, IndexSessionFileOutcome.Indexed, document.Chunks.Count, document.ParseOffset);
    }

    /// <summary>
    /// Reads the appended tail and either adds what it found or, when it held no new message,
    /// settles for resolving the title again and moving the watermark on.
    /// </summary>
    private async Task<IndexSessionFileResult> ResumeAsync(
        SessionFile file,
        IndexedSession stored,
        long offset,
        CancellationToken cancellationToken)
    {
        await using var transcript = await transcripts
            .OpenAsync(file.FilePath, offset, cancellationToken)
            .ConfigureAwait(false);

        var request = new TranscriptParseRequest
        {
            SessionId = file.SessionId,
            FileName = file.FileName,
            Content = transcript.Content,
            StartOffset = offset,
            KnownTitles = stored.TitleCandidates,
            KnownFolder = stored.Folder.IsKnown ? stored.Folder : null,
        };

        var document = await parser.ParseAsync(request, cancellationToken).ConfigureAwait(false);
        var bytesRead = document.ParseOffset - offset;

        return HoldsNewMessage(document)
            ? await AppendAsync(file, transcript, document, bytesRead, cancellationToken).ConfigureAwait(false)
            : await RefreshTitleAsync(file, transcript, document, bytesRead, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IndexSessionFileResult> AppendAsync(
        SessionFile file,
        OpenTranscript transcript,
        SessionDocument document,
        long bytesRead,
        CancellationToken cancellationToken)
    {
        await writer
            .AppendAsync(BuildEntry(file, transcript, document), cancellationToken)
            .ConfigureAwait(false);

        IndexSessionFileLog.TranscriptAppended(
            logger,
            file.SessionId,
            document.Chunks.Count,
            bytesRead,
            document.ParseOffset);

        return Describe(file, document, IndexSessionFileOutcome.Appended, document.Chunks.Count, bytesRead);
    }

    private async Task<IndexSessionFileResult> RefreshTitleAsync(
        SessionFile file,
        OpenTranscript transcript,
        SessionDocument document,
        long bytesRead,
        CancellationToken cancellationToken)
    {
        var revision = new SessionTitleRevision
        {
            SessionId = document.SessionId,
            Title = document.Title,
            Candidates = document.TitleCandidates,
            Fingerprint = transcript.ToFingerprint(document.ParseOffset),
            LastActivity = document.LastActivity,
            IndexedAt = timeProvider.GetUtcNow(),
        };

        var revised = await writer.ReviseTitleAsync(revision, cancellationToken).ConfigureAwait(false);

        if (!revised)
        {
            return await ReadWholeAsync(file, cancellationToken).ConfigureAwait(false);
        }

        IndexSessionFileLog.TitleRefreshed(logger, file.SessionId, document.Title.Source, document.ParseOffset);

        return Describe(file, document, IndexSessionFileOutcome.TitleRefreshed, chunksWritten: 1, bytesRead);
    }

    /// <summary>
    /// Whether the tail held anything beyond what is rebuilt from the session row anyway. The
    /// title and the folder are produced by every pass, resumed or not, so their presence says
    /// nothing about whether the conversation moved on.
    /// </summary>
    private static bool HoldsNewMessage(SessionDocument document) =>
        document.Chunks.Any(chunk => chunk.Kind is not (ChunkKind.Title or ChunkKind.Folder));

    private static IndexSessionFileResult Describe(
        SessionFile file,
        SessionDocument document,
        IndexSessionFileOutcome outcome,
        int chunksWritten,
        long bytesRead) => new()
        {
            SessionId = file.SessionId,
            Outcome = outcome,
            ChunksWritten = chunksWritten,
            BytesRead = bytesRead,
            ParseOffset = document.ParseOffset,
            ParseError = document.ParseError,
        };

    private static IndexSessionFileResult Missing(SessionFile file) => new()
    {
        SessionId = file.SessionId,
        Outcome = IndexSessionFileOutcome.Missing,
    };

    private IndexSessionFileResult Unreadable(SessionFile file, Exception exception)
    {
        IndexSessionFileLog.TranscriptUnreadable(logger, exception, file.FilePath);

        return new IndexSessionFileResult
        {
            SessionId = file.SessionId,
            Outcome = IndexSessionFileOutcome.Failed,
        };
    }

    private SessionIndexEntry BuildEntry(SessionFile file, OpenTranscript transcript, SessionDocument document) => new()
    {
        Document = document,
        FilePath = file.FilePath,
        Fingerprint = transcript.ToFingerprint(document.ParseOffset),
        IndexedAt = timeProvider.GetUtcNow(),
    };
}
