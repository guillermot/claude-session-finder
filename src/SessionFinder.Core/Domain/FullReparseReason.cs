namespace SessionFinder.Core.Domain;

/// <summary>
/// Why a transcript has to be read from offset zero again. Carried so the decision shows up in the
/// log instead of appearing as an unexplained burst of work.
/// </summary>
public enum FullReparseReason
{
    /// <summary>The file has never been indexed.</summary>
    NeverIndexed = 0,

    /// <summary>The file is now shorter than the offset the last pass stopped at.</summary>
    FileTruncated = 1,

    /// <summary>The leading bytes changed, so the file was rewritten rather than appended to.</summary>
    HeadChanged = 2,

    /// <summary>
    /// The caller asked for the whole file to be read. The watermark is not consulted at all,
    /// because the reason to ask is that it is suspected of being wrong.
    /// </summary>
    Forced = 3,
}
