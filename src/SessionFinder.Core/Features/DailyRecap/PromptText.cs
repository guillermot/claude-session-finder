using System.Text;
using System.Text.RegularExpressions;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Separates what the person typed from what the harness wrapped around it.
/// </summary>
/// <remarks>
/// <para>
/// A recorded human prompt is frequently not human at all. Measured over a real index, the prompts
/// that open with a tag include the editor's selection and open-file notices, slash-command
/// envelopes, local command output, background task notifications and injected instructions, and
/// a cancelled turn is recorded as <c>[Request interrupted by user]</c>. Every one of those would
/// count as work done, and the editor notices would even pass for the most detailed prompt of the
/// day.
/// </para>
/// <para>
/// The rule is structural rather than a list of tag names: any lowercase paired tag is an envelope
/// and is removed with its contents. A tag the harness adds next year is then handled the day it
/// appears, and what remains is exactly the text the person wrote around the envelopes, if any.
/// </para>
/// </remarks>
public static partial class PromptText
{
    private const string Ellipsis = "…";
    private const int MatchTimeoutMilliseconds = 250;

    private static readonly string[] NoisePrefixes =
    [
        "[Request interrupted",
        "Caveat: The messages below",
    ];

    /// <summary>
    /// Reduces a recorded prompt to what the person typed.
    /// </summary>
    /// <param name="raw">The prompt as recorded.</param>
    /// <returns>The prompt on one line, or <see langword="null"/> when nothing human is left.</returns>
    public static string? Clean(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var collapsed = Collapse(StripEnvelopes(raw));

        if (collapsed.Length == 0)
        {
            return null;
        }

        return NoisePrefixes.Any(prefix => collapsed.StartsWith(prefix, StringComparison.Ordinal))
            ? null
            : collapsed;
    }

    /// <summary>
    /// Flattens every run of whitespace to one space, so a multi-line message reads as one line.
    /// </summary>
    /// <param name="text">Any text.</param>
    /// <returns>The text on one line, trimmed.</returns>
    public static string Collapse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Shortens text to a length, ending on a word where one is near.
    /// </summary>
    /// <param name="text">Text already on one line.</param>
    /// <param name="maximumLength">Longest result, before the ellipsis.</param>
    /// <returns>The text, or a prefix of it followed by an ellipsis.</returns>
    public static string Truncate(string text, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= maximumLength)
        {
            return text;
        }

        var cut = maximumLength;

        if (char.IsHighSurrogate(text[cut - 1]))
        {
            cut--;
        }

        var lastSpace = text.LastIndexOf(' ', cut - 1);

        if (lastSpace > maximumLength * 3 / 4)
        {
            cut = lastSpace;
        }

        return string.Concat(text.AsSpan(0, cut).TrimEnd(), Ellipsis);
    }

    /// <summary>
    /// Removes every paired envelope. A pathological message that trips the match timeout is kept
    /// whole rather than lost: an envelope shown by mistake costs a line, a prompt dropped by
    /// mistake costs the work it describes.
    /// </summary>
    private static string StripEnvelopes(string raw)
    {
        try
        {
            return Envelope().Replace(raw, " ");
        }
        catch (RegexMatchTimeoutException)
        {
            return raw;
        }
    }

    [GeneratedRegex(
        @"<(?<tag>[a-z][a-z0-9_-]*)(?:\s[^>]*)?>.*?</\k<tag>\s*>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        MatchTimeoutMilliseconds)]
    private static partial Regex Envelope();
}
