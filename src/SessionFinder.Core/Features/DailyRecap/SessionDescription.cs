using System.Text.RegularExpressions;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Writes the few lines under a session's title that say what the session achieved, in words a
/// stand-up can use.
/// </summary>
/// <remarks>
/// <para>
/// The best account of a session is usually the assistant's last reply, but it is written for the
/// person at the keyboard, not for a meeting: it opens with "Done." or "All set!", ends by offering
/// more work, and marks up code in Markdown. Those parts are removed, and what is left is the
/// outcome itself.
/// </para>
/// <para>
/// When there is no reply, or what is left of it is too thin to say anything — a question back to
/// the user, a one-word acknowledgement — the description falls back to what the person asked for,
/// which at least names the work.
/// </para>
/// </remarks>
public static partial class SessionDescription
{
    /// <summary>The longest description, before an ellipsis is added.</summary>
    public const int MaximumLength = 320;

    private const int ShortestUsefulOutcome = 25;
    private const int MatchTimeoutMilliseconds = 250;

    /// <summary>
    /// Describes a session.
    /// </summary>
    /// <param name="session">The session's account of a day.</param>
    /// <returns>The description, or <see langword="null"/> when there is nothing to say.</returns>
    public static string? For(SessionRecap session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var outcome = Clean(session.Outcome);

        if (outcome is { Length: >= ShortestUsefulOutcome } && !outcome.EndsWith('?'))
        {
            return Finish(outcome);
        }

        var asked = Clean(session.KeyPrompts.FirstOrDefault());

        return asked is { Length: > 0 } ? Finish(asked) : outcome is { Length: > 0 } ? Finish(outcome) : null;
    }

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var plain = PromptText.Collapse(Markup().Replace(text, string.Empty));
            var withoutOffer = ClosingOffer().Replace(plain, string.Empty).Trim();
            var previous = string.Empty;

            while (previous != withoutOffer)
            {
                previous = withoutOffer;
                withoutOffer = LeadingFiller().Replace(withoutOffer, string.Empty).Trim();
            }

            return withoutOffer.Length == 0 ? null : withoutOffer;
        }
        catch (RegexMatchTimeoutException)
        {
            return PromptText.Collapse(text);
        }
    }

    private static string Finish(string text)
    {
        var sentence = char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;

        return PromptText.Truncate(sentence, MaximumLength);
    }

    [GeneratedRegex(@"\*\*|`|^#+\s*", RegexOptions.Multiline | RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex Markup();

    [GeneratedRegex(
        @"^(?:done|all done|all set|finished|great|perfect|nice|sure|ok(?:ay)?|got it|fixed|here(?:'s| is) (?:what|the summary)[^:.]*)\s*[.:!,—-]+\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex LeadingFiller();

    [GeneratedRegex(
        @"(?:^|(?<=[.!?])\s+)(?:let me know|want me to|would you like|should i|shall i|if you(?:'d)? (?:want|like)|do you want)\b.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex ClosingOffer();
}
