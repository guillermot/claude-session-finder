using System.Text;

namespace SessionFinder.Core.Domain;

/// <summary>
/// The line the user reads in the result list, together with the reason it says what it says.
/// </summary>
/// <param name="Text">The title to show.</param>
/// <param name="Source">The candidate that won.</param>
public sealed record SessionTitle(string Text, TitleSource Source)
{
    /// <summary>Longest title derived from a prompt, before an ellipsis is appended.</summary>
    public const int MaxPromptTitleLength = 80;

    private const string Ellipsis = "…";
    private const string UntitledText = "Untitled session";

    /// <summary>
    /// Applies the four-level precedence to a candidate set.
    /// </summary>
    /// <param name="candidates">Everything known about the session's possible titles.</param>
    /// <returns>
    /// The winning title: a user-typed title, else a generated one, else a truncated first prompt,
    /// else the file name. The result is pure, so a later pass can call this again with merged
    /// candidates and replace the stored title.
    /// </returns>
    public static SessionTitle Resolve(SessionTitleCandidates candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (Collapse(candidates.CustomTitle) is { } custom)
        {
            return new SessionTitle(Truncate(custom), TitleSource.CustomTitle);
        }

        if (Collapse(candidates.AiTitle) is { } generated)
        {
            return new SessionTitle(Truncate(generated), TitleSource.AiTitle);
        }

        if (Collapse(candidates.FirstPrompt) is { } prompt)
        {
            return new SessionTitle(Truncate(prompt), TitleSource.FirstPrompt);
        }

        if (Collapse(candidates.FileName) is { } fileName)
        {
            return new SessionTitle(Truncate(fileName), TitleSource.FileName);
        }

        return new SessionTitle(UntitledText, TitleSource.Unknown);
    }

    /// <summary>Renders the title text.</summary>
    /// <returns><see cref="Text"/>.</returns>
    public override string ToString() => Text;

    /// <summary>
    /// Flattens the runs of whitespace that make a multi-line prompt useless as a one-line title,
    /// and reports blank input as absent rather than as an empty title.
    /// </summary>
    private static string? Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var character in value)
        {
            var isWhitespace = char.IsWhiteSpace(character) || char.IsControl(character);

            if (isWhitespace)
            {
                previousWasWhitespace = builder.Length > 0;
                continue;
            }

            if (previousWasWhitespace)
            {
                builder.Append(' ');
                previousWasWhitespace = false;
            }

            builder.Append(character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaxPromptTitleLength)
        {
            return value;
        }

        var cut = MaxPromptTitleLength;

        if (char.IsHighSurrogate(value[cut - 1]))
        {
            cut--;
        }

        return string.Concat(value.AsSpan(0, cut).TrimEnd(), Ellipsis);
    }
}
