namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// What a reconcile pass did, in numbers.
/// </summary>
public sealed record ReconcileIndexResult
{
    /// <summary>How many transcripts the catalogue found.</summary>
    public required int FilesDiscovered { get; init; }

    /// <summary>How many transcripts were read and written to the index.</summary>
    public required int SessionsIndexed { get; init; }

    /// <summary>How many transcripts the watermark let the pass leave alone.</summary>
    public required int SessionsSkipped { get; init; }

    /// <summary>How many indexed sessions no longer had a transcript and were removed.</summary>
    public required int SessionsPruned { get; init; }

    /// <summary>How many chunks were written across every indexed session.</summary>
    public required int ChunksWritten { get; init; }

    /// <summary>How many bytes of transcript were read.</summary>
    public required long BytesRead { get; init; }

    /// <summary>How many sessions were indexed with damaged lines skipped.</summary>
    public required int SessionsWithParseErrors { get; init; }

    /// <summary>How many transcripts could not be read at all.</summary>
    public required int FilesFailed { get; init; }

    /// <summary>How long the pass took, end to end.</summary>
    public required TimeSpan Elapsed { get; init; }
}
