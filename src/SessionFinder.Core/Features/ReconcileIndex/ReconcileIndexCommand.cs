namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// Asks for the index to be brought in step with what is on disk.
/// </summary>
public sealed record ReconcileIndexCommand
{
    /// <summary>A pass that leaves unchanged transcripts alone.</summary>
    public static ReconcileIndexCommand Incremental { get; } = new();

    /// <summary>
    /// Reads every transcript from the start, even the ones the watermark says are unchanged.
    /// The escape hatch for an index suspected of being wrong.
    /// </summary>
    public bool ForceFullReparse { get; init; }

    /// <summary>
    /// Receives one report per transcript, so a long pass can show where it is. Never carries
    /// transcript text.
    /// </summary>
    public IProgress<ReconcileIndexProgress>? Progress { get; init; }
}
