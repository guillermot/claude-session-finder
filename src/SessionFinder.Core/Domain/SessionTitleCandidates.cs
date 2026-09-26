namespace SessionFinder.Core.Domain;

/// <summary>
/// Everything a pass over a transcript learned that could serve as a title, kept separate from the
/// resolved <see cref="SessionTitle"/> so the decision can be made again later.
/// </summary>
/// <remarks>
/// Title records are appended to the transcript long after the messages they describe, and a
/// session can be renamed repeatedly. An incremental pass that reads only the new tail must be
/// able to combine what it just saw with what was already known, which is what
/// <see cref="MergeWith"/> is for.
/// </remarks>
public sealed record SessionTitleCandidates
{
    /// <summary>The transcript file name, the candidate of last resort. Always present.</summary>
    public required string FileName { get; init; }

    /// <summary>The most recent <c>custom-title</c> seen, if any.</summary>
    public string? CustomTitle { get; init; }

    /// <summary>The most recent <c>ai-title</c> seen, if any.</summary>
    public string? AiTitle { get; init; }

    /// <summary>The first human prompt of the session, untruncated.</summary>
    public string? FirstPrompt { get; init; }

    /// <summary>
    /// Creates a candidate set that knows nothing but the file name.
    /// </summary>
    /// <param name="fileName">The transcript file name.</param>
    /// <returns>A candidate set with only the fallback filled in.</returns>
    public static SessionTitleCandidates ForFile(string fileName) => new() { FileName = fileName };

    /// <summary>
    /// Combines this set with candidates discovered by a later pass over the same transcript.
    /// </summary>
    /// <param name="later">What the later pass found.</param>
    /// <returns>
    /// A set where a renamed session wins — a later <c>custom-title</c> or <c>ai-title</c> replaces
    /// the earlier one — while the first prompt stays the first prompt, because a later pass over
    /// the tail of a file can never see an earlier one.
    /// </returns>
    public SessionTitleCandidates MergeWith(SessionTitleCandidates later)
    {
        ArgumentNullException.ThrowIfNull(later);

        return new SessionTitleCandidates
        {
            FileName = later.FileName,
            CustomTitle = later.CustomTitle ?? CustomTitle,
            AiTitle = later.AiTitle ?? AiTitle,
            FirstPrompt = FirstPrompt ?? later.FirstPrompt,
        };
    }
}
