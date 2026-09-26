namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// Brings the index in step with the transcripts on disk.
/// </summary>
public interface IReconcileIndexHandler
{
    /// <summary>
    /// Runs one reconcile pass.
    /// </summary>
    /// <param name="command">Whether to force a full read, and where to report progress.</param>
    /// <param name="cancellationToken">Checked between transcripts, never mid-transaction.</param>
    /// <returns>What the pass did.</returns>
    Task<ReconcileIndexResult> HandleAsync(ReconcileIndexCommand command, CancellationToken cancellationToken);
}
