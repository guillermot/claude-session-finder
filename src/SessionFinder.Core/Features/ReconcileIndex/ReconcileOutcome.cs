namespace SessionFinder.Core.Features.ReconcileIndex;

/// <summary>
/// What a reconcile pass did with one transcript.
/// </summary>
public enum ReconcileOutcome
{
    /// <summary>The transcript was read and its session rewritten.</summary>
    Indexed = 0,

    /// <summary>The watermark matched the file on disk, so the file was never opened.</summary>
    Skipped = 1,

    /// <summary>
    /// The transcript was read and indexed, but complete lines in it were not well-formed JSON and
    /// were skipped. The session is still searchable; the row carries the error.
    /// </summary>
    IndexedWithParseError = 2,

    /// <summary>
    /// The transcript could not be read at all. Logged and counted; the pass carries on with the
    /// next file, because one unreadable transcript must not cost the user the other ninety-five.
    /// </summary>
    Failed = 3,
}
