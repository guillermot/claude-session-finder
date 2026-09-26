using System.Globalization;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// Folds a sequence of transcript lines into a <see cref="SessionDocument"/>.
/// </summary>
/// <remarks>
/// Kept apart from the parser so the parser owns only the read loop and the offset discipline,
/// and this type owns only the rules about what a transcript means.
/// </remarks>
internal sealed class TranscriptAccumulator
{
    private const string ParseErrorFormat = "{0} line(s) skipped: not well-formed JSON.";

    private readonly TranscriptParseRequest _request;
    private readonly List<SearchChunk> _messageChunks = [];
    private readonly List<string> _textBlocks = [];

    private WorkingFolder? _rootFolder;
    private WorkingFolder? _anyFolder;
    private string? _rootBranch;
    private string? _anyBranch;
    private string? _customTitle;
    private string? _generatedTitle;
    private string? _lastPrompt;
    private string? _firstPrompt;
    private DateTimeOffset? _firstActivity;
    private DateTimeOffset? _lastActivity;
    private int _messageCount;
    private int _malformedLines;

    /// <summary>
    /// Creates an accumulator for one pass.
    /// </summary>
    /// <param name="request">The work item, including anything an earlier pass already knew.</param>
    public TranscriptAccumulator(TranscriptParseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _request = request;
    }

    /// <summary>
    /// Folds one line into the accumulated state.
    /// </summary>
    /// <param name="line">The raw bytes of a complete line, without its newline.</param>
    public void Accept(ReadOnlySpan<byte> line)
    {
        if (line.IsEmpty)
        {
            return;
        }

        if (!TranscriptRecordScanner.TryScan(line, out var record))
        {
            _malformedLines++;
            return;
        }

        TrackActivity(record);
        TrackFolder(record);

        switch (record.Type)
        {
            case TranscriptRecordType.CustomTitle:
                _customTitle = record.TitleText ?? _customTitle;
                break;
            case TranscriptRecordType.AiTitle:
                _generatedTitle = record.TitleText ?? _generatedTitle;
                break;
            case TranscriptRecordType.LastPrompt:
                _lastPrompt = record.TitleText ?? _lastPrompt;
                break;
            case TranscriptRecordType.User:
                AcceptUser(line, record);
                break;
            case TranscriptRecordType.Assistant:
                AcceptAssistant(line, record);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Produces the document for this pass.
    /// </summary>
    /// <param name="parseOffset">The offset the read loop committed to.</param>
    /// <returns>The document, with the title and folder chunks ahead of the message chunks.</returns>
    public SessionDocument Build(long parseOffset)
    {
        var candidates = ResolveCandidates();
        var title = SessionTitle.Resolve(candidates);
        var folder = ResolveFolder();
        var branch = _rootBranch ?? _anyBranch;

        return new SessionDocument
        {
            SessionId = _request.SessionId,
            Title = title,
            TitleCandidates = candidates,
            Folder = folder,
            GitBranch = branch,
            LastPrompt = _lastPrompt,
            FirstActivity = _firstActivity,
            LastActivity = _lastActivity,
            MessageCount = _messageCount,
            Chunks = BuildChunks(title, folder, branch),
            ParseOffset = parseOffset,
            ParseError = _malformedLines == 0
                ? null
                : string.Format(CultureInfo.InvariantCulture, ParseErrorFormat, _malformedLines),
        };
    }

    private void AcceptUser(ReadOnlySpan<byte> line, TranscriptRecord record)
    {
        if (record.HasToolUseResult || record.IsMeta || !record.HasMessage)
        {
            return;
        }

        _messageCount++;

        foreach (var text in ReadTextBlocks(line, record))
        {
            _firstPrompt ??= text;
            ChunkSplitter.Append(_messageChunks, ChunkKind.UserPrompt, text, record.Timestamp);
        }
    }

    private void AcceptAssistant(ReadOnlySpan<byte> line, TranscriptRecord record)
    {
        if (!record.HasMessage)
        {
            return;
        }

        _messageCount++;

        foreach (var text in ReadTextBlocks(line, record))
        {
            ChunkSplitter.Append(_messageChunks, ChunkKind.AssistantText, text, record.Timestamp);
        }
    }

    private List<string> ReadTextBlocks(ReadOnlySpan<byte> line, TranscriptRecord record)
    {
        _textBlocks.Clear();

        if (!MessageContentReader.TryReadTextBlocks(line.Slice(record.MessageStart, record.MessageLength), _textBlocks))
        {
            _malformedLines++;
        }

        return _textBlocks;
    }

    private void TrackActivity(TranscriptRecord record)
    {
        if (record.Timestamp is not { } timestamp)
        {
            return;
        }

        if (_firstActivity is null || timestamp < _firstActivity)
        {
            _firstActivity = timestamp;
        }

        if (_lastActivity is null || timestamp > _lastActivity)
        {
            _lastActivity = timestamp;
        }
    }

    /// <summary>
    /// Remembers the working directory of the record that opens the conversation, and separately
    /// the first working directory of any kind. The first is authoritative because <c>cwd</c>
    /// drifts as the session runs; the second only exists so a transcript missing its root record
    /// still reports something.
    /// </summary>
    private void TrackFolder(TranscriptRecord record)
    {
        if (record.WorkingDirectory is null)
        {
            return;
        }

        if (_anyFolder is null)
        {
            _anyFolder = WorkingFolder.FromTranscriptCwd(record.WorkingDirectory);
            _anyBranch = record.GitBranch;
        }

        if (_rootFolder is not null || record.Type != TranscriptRecordType.User || record.HasParent)
        {
            return;
        }

        _rootFolder = WorkingFolder.FromTranscriptCwd(record.WorkingDirectory);
        _rootBranch = record.GitBranch;
    }

    /// <summary>
    /// Prefers the folder this pass found at the root of the conversation, then whatever an
    /// earlier pass had established, and only then any working directory seen in this pass: a
    /// resumed pass starts after the root record, so its first sighting is not authoritative.
    /// </summary>
    private WorkingFolder ResolveFolder() =>
        _rootFolder ?? _request.KnownFolder ?? _anyFolder ?? WorkingFolder.Unknown;

    private SessionTitleCandidates ResolveCandidates()
    {
        var known = _request.KnownTitles ?? SessionTitleCandidates.ForFile(_request.FileName);

        return known.MergeWith(new SessionTitleCandidates
        {
            FileName = _request.FileName,
            CustomTitle = _customTitle,
            AiTitle = _generatedTitle,
            FirstPrompt = _firstPrompt,
        });
    }

    private List<SearchChunk> BuildChunks(SessionTitle title, WorkingFolder folder, string? branch)
    {
        var chunks = new List<SearchChunk>(_messageChunks.Count + 3)
        {
            new(ChunkKind.Title, ChunkSplitter.Cap(title.Text), _lastActivity),
        };

        if (folder.IsKnown)
        {
            chunks.Add(new SearchChunk(
                ChunkKind.Folder,
                ChunkSplitter.Cap(BuildFolderText(folder, branch)),
                _lastActivity));
        }

        if (!string.IsNullOrWhiteSpace(_lastPrompt))
        {
            ChunkSplitter.Append(chunks, ChunkKind.LastPrompt, _lastPrompt, _lastActivity);
        }

        chunks.AddRange(_messageChunks);

        return chunks;
    }

    private static string BuildFolderText(WorkingFolder folder, string? branch) =>
        string.IsNullOrWhiteSpace(branch) ? folder.Display : $"{folder.Display} {branch}";

}
