using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.IndexSessionFile;

/// <summary>
/// Asks for one transcript to be brought into the index.
/// </summary>
public sealed record IndexSessionFileCommand
{
    /// <summary>The transcript, as the catalogue measured it.</summary>
    public required SessionFile File { get; init; }

    /// <summary>
    /// Reads the transcript from the start even when the watermark says it has not changed, and
    /// even when it says only the tail is new.
    /// </summary>
    public bool ForceFullReparse { get; init; }
}
