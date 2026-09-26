namespace SessionFinder.Core.Features.IndexSessionFile;

/// <summary>
/// What indexing one transcript did, which is the same thing as which branch of the parse plan ran.
/// </summary>
public enum IndexSessionFileOutcome
{
    /// <summary>The watermark matched the file on disk, so the file was never opened.</summary>
    Skipped = 0,

    /// <summary>The transcript was read from the start and its session rewritten.</summary>
    Indexed = 1,

    /// <summary>Only the bytes appended since the last pass were read, and their chunks added.</summary>
    Appended = 2,

    /// <summary>
    /// Only the bytes appended since the last pass were read, and they held no new message: the
    /// title was resolved again and the watermark moved on.
    /// </summary>
    TitleRefreshed = 3,

    /// <summary>
    /// The transcript could not be read. Logged and counted; the session is left as it was,
    /// because one unreadable transcript must not cost the user the others.
    /// </summary>
    Failed = 4,

    /// <summary>The transcript is no longer on disk, so there is nothing to index.</summary>
    Missing = 5,
}
