namespace SessionFinder.Core.Domain;

/// <summary>
/// Decides, from two fingerprints and nothing else, whether a transcript is skipped, resumed or
/// read again from the start.
/// </summary>
/// <remarks>
/// This is the logic that rots silently: get it wrong towards skipping and new messages never
/// appear, get it wrong towards reparsing and every pass re-reads 183 MB. Keeping it pure and
/// free of I/O is what makes both failure modes testable.
/// </remarks>
public static class ParsePlanner
{
    /// <summary>
    /// Chooses the plan for one file.
    /// </summary>
    /// <param name="stored">
    /// The fingerprint written by the last successful pass, or <see langword="null"/> when the file
    /// has never been indexed.
    /// </param>
    /// <param name="current">The fingerprint just measured on disk.</param>
    /// <returns>The plan to execute.</returns>
    public static ParsePlan Decide(FileFingerprint? stored, FileFingerprint current)
    {
        if (stored is not { } previous)
        {
            return new ParsePlan.FullReparse(FullReparseReason.NeverIndexed);
        }

        if (IsUnchanged(previous, current))
        {
            return ParsePlan.Skip.Instance;
        }

        if (current.Size < previous.ParseOffset)
        {
            return new ParsePlan.FullReparse(FullReparseReason.FileTruncated);
        }

        if (HasDifferentHead(previous, current))
        {
            return new ParsePlan.FullReparse(FullReparseReason.HeadChanged);
        }

        return new ParsePlan.Resume(previous.ParseOffset);
    }

    private static bool IsUnchanged(FileFingerprint previous, FileFingerprint current) =>
        previous.Size == current.Size && previous.MTimeTicks == current.MTimeTicks;

    /// <summary>
    /// A head hash is only evidence when both sides have one: an index written before hashing
    /// existed must not be mistaken for a rewritten file.
    /// </summary>
    private static bool HasDifferentHead(FileFingerprint previous, FileFingerprint current) =>
        previous.HeadSha256 is { } storedHash
        && current.HeadSha256 is { } currentHash
        && !string.Equals(storedHash, currentHash, StringComparison.OrdinalIgnoreCase);
}
