using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// What one session amounted to on one day.
/// </summary>
public sealed record SessionRecap
{
    /// <summary>The session.</summary>
    public required SessionId SessionId { get; init; }

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
}
