using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// One transcript's worth of progress through a reconcile pass.
/// </summary>
/// <param name="Completed">How many transcripts have been dealt with, including this one.</param>
/// <param name="Total">How many transcripts the pass found.</param>
/// <param name="SessionId">The session just dealt with.</param>
/// <param name="Outcome">What was done with it.</param>
public sealed record ReconcileIndexProgress(
    int Completed,
    int Total,
    SessionId SessionId,
    ReconcileOutcome Outcome);
