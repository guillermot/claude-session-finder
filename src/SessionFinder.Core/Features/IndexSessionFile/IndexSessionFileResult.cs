using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.IndexSessionFile;

/// <summary>
/// What one transcript cost and produced, in the terms a caller aggregates or prints.
/// </summary>
public sealed record IndexSessionFileResult
{
    /// <summary>The session the transcript belongs to.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>Which branch of the parse plan ran.</summary>
    public required IndexSessionFileOutcome Outcome { get; init; }

    /// <summary>How many chunks were written, whether replacing or appending.</summary>
    public int ChunksWritten { get; init; }

    /// <summary>
    /// How many bytes of transcript this pass read, which for a resumed pass is the appended tail
    /// and not the file.
    /// </summary>
    public long BytesRead { get; init; }

    /// <summary>The offset the pass committed to, and the one the next pass will resume from.</summary>
    public long ParseOffset { get; init; }

    /// <summary>Set when complete lines were skipped because they were not well-formed JSON.</summary>
    public string? ParseError { get; init; }
}
