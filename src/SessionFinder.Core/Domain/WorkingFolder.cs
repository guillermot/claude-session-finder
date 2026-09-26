namespace SessionFinder.Core.Domain;

/// <summary>
/// The folder a session was started from, carrying both the spelling to show and the spelling to
/// group by.
/// </summary>
/// <remarks>
/// <para>
/// A single real transcript reports its <c>cwd</c> as <c>C:\git\Project</c> 613 times,
/// <c>c:\git\Project</c> 560 times and <c>C:\git\Project\src</c> 466 times. Grouping on the raw
/// string therefore splits one folder into several, and lowercasing everything makes the folder
/// unpleasant to read in the result list.
/// </para>
/// <para>
/// The rule this type encodes: <see cref="Display"/> keeps the casing of the first sighting and
/// only loses trailing separators; <see cref="Key"/> is the case-folded, separator-folded form and
/// is the only value that may be compared, grouped or excluded on.
/// </para>
/// </remarks>
public sealed record WorkingFolder
{
    private const char PosixSeparator = '/';
    private const char WindowsSeparator = '\\';
    private const char DriveMarker = ':';

    private WorkingFolder(string display, string key, FolderSource source)
    {
        Display = display;
        Key = key;
        Source = source;
    }

    /// <summary>A folder that could not be recovered from any source.</summary>
    public static WorkingFolder Unknown { get; } = new(string.Empty, string.Empty, FolderSource.Unknown);

    /// <summary>The path as first seen, minus trailing separators. Never case-folded.</summary>
    public string Display { get; }

    /// <summary>The case-folded, separator-folded path. The only form safe to group or compare on.</summary>
    public string Key { get; }

    /// <summary>Which recovery strategy produced this folder.</summary>
    public FolderSource Source { get; }

    /// <summary>Whether a real folder is known, as opposed to <see cref="Unknown"/>.</summary>
    public bool IsKnown => Source != FolderSource.Unknown;

    /// <summary>
    /// Builds a folder from the <c>cwd</c> field of a transcript record.
    /// </summary>
    /// <param name="cwd">The raw <c>cwd</c> value; blank input yields <see cref="Unknown"/>.</param>
    /// <returns>The normalised folder.</returns>
    public static WorkingFolder FromTranscriptCwd(string? cwd) => From(cwd, FolderSource.TranscriptCwd);

    /// <summary>
    /// Builds a folder from the <c>project</c> path recorded in <c>history.jsonl</c>.
    /// </summary>
    /// <param name="path">The raw path; blank input yields <see cref="Unknown"/>.</param>
    /// <returns>The normalised folder.</returns>
    public static WorkingFolder FromHistoryFile(string? path) => From(path, FolderSource.HistoryFile);

    /// <summary>
    /// Builds a folder from a raw path, tagging it with the strategy that found it.
    /// </summary>
    /// <param name="path">The raw path; blank input yields <see cref="Unknown"/>.</param>
    /// <param name="source">The recovery strategy that produced <paramref name="path"/>.</param>
    /// <returns>The normalised folder, or <see cref="Unknown"/> when there was nothing to normalise.</returns>
    public static WorkingFolder From(string? path, FolderSource source)
    {
        if (source == FolderSource.Unknown || string.IsNullOrWhiteSpace(path))
        {
            return Unknown;
        }

        var display = NormalizeDisplay(path);

        return display.Length == 0 ? Unknown : new WorkingFolder(display, NormalizeKey(display), source);
    }

    /// <summary>
    /// Produces the grouping key for an already-normalised display path.
    /// </summary>
    /// <param name="display">A display path.</param>
    /// <returns>The case-folded, separator-folded key.</returns>
    public static string NormalizeKey(string display)
    {
        ArgumentNullException.ThrowIfNull(display);

        return display.Replace(PosixSeparator, WindowsSeparator).ToUpperInvariant();
    }

    /// <summary>Renders the folder the way it should be shown to the user.</summary>
    /// <returns>The display path, or an empty string when the folder is unknown.</returns>
    public override string ToString() => Display;

    private static string NormalizeDisplay(string path)
    {
        var trimmed = path.Trim().TrimEnd(WindowsSeparator, PosixSeparator);

        if (trimmed.Length == 0)
        {
            return RestoreRoot(path);
        }

        return EndsAtDriveLetter(trimmed) ? trimmed + WindowsSeparator : trimmed;
    }

    /// <summary>
    /// A path made entirely of separators is a filesystem root, not an empty path, so one leading
    /// separator has to survive the trim, in whichever flavour the caller used.
    /// </summary>
    private static string RestoreRoot(string path)
    {
        var trimmed = path.Trim();

        return trimmed.Length == 0 ? string.Empty : trimmed[..1];
    }

    /// <summary>
    /// Trimming <c>C:\</c> down to <c>C:</c> would change its meaning from the drive root to the
    /// drive's current directory, so the separator is put back.
    /// </summary>
    private static bool EndsAtDriveLetter(string trimmed) =>
        trimmed.Length == 2 && trimmed[1] == DriveMarker && char.IsLetter(trimmed[0]);
}
