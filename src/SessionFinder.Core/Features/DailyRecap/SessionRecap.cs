using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// What one session amounted to on one day.
/// </summary>
public sealed record SessionRecap
{
    /// <summary>
    /// The most prompts a session can have and still count as a quick question rather than work.
    /// </summary>
    /// <remarks>
    /// Measured on a real history, sessions of one or two prompts are almost all asides — what a
    /// keyword means, where a folder came from — and listing them beside the day's real work buries
    /// it. They are folded, not dropped: the recap still says how many there were.
    /// </remarks>
    public const int QuickPromptLimit = 2;

    /// <summary>The session.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The folder the session ran in, which is what resuming or revealing it needs.</summary>
    public WorkingFolder Folder { get; init; } = WorkingFolder.Unknown;

    /// <summary>The title shown for the session, which is usually the best one-line account of it.</summary>
    public required string Title { get; init; }

    /// <summary>The git branch the session recorded, when it was a real branch.</summary>
    public string? Branch { get; init; }

    /// <summary>How many prompts the person typed that day, envelopes and interruptions excluded.</summary>
    public required int PromptCount { get; init; }

    /// <summary>The first message of the day, in local time.</summary>
    public required DateTimeOffset FirstActivity { get; init; }

    /// <summary>The last message of the day, in local time.</summary>
    public required DateTimeOffset LastActivity { get; init; }

    /// <summary>
    /// The most substantial prompts of the day, oldest first, each on one line and shortened. The
    /// longest prompts are chosen because "yes, do it" says nothing about the work.
    /// </summary>
    public IReadOnlyList<string> KeyPrompts { get; init; } = [];

    /// <summary>
    /// The last thing the assistant said that day, shortened, which is usually its account of what
    /// was done. Only read for the focus day, and only ever used as input to a summary.
    /// </summary>
    public string? Outcome { get; init; }

    /// <summary>Whether the session was a quick question rather than a piece of work.</summary>
    public bool IsQuick => PromptCount <= QuickPromptLimit;
}
