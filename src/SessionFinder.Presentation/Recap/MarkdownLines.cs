using System.Text;

namespace SessionFinder.Presentation.Recap;

/// <summary>
/// Reads the small part of Markdown the recap and its summary are written in — headings, nested
/// bullets, bold and inline code — into lines a window can draw with real styles.
/// </summary>
/// <remarks>
/// <para>
/// A full Markdown renderer would be a dependency for both heads, and neither UI framework has one
/// built in. The recap is written by this application and the summary is asked for in exactly this
/// subset, so a parser for the subset is the whole of what is needed. Anything outside it is shown
/// as the text it is, never dropped.
/// </para>
/// <para>
/// The Markdown itself stays the source of truth: it is what the copy command puts on the
/// clipboard, because a chat tool renders it and a person reading it raw can still follow it.
/// </para>
/// </remarks>
public static class MarkdownLines
{
    private const int SpacesPerIndent = 2;
    private const string Bold = "**";
    private const char Code = '`';
    private const char Heading = '#';

    /// <summary>
    /// Splits Markdown into styled lines.
    /// </summary>
    /// <param name="markdown">The text.</param>
    /// <returns>One entry per line of the input.</returns>
    public static IReadOnlyList<MarkdownLine> Parse(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return [];
        }

        return
        [
            .. markdown
                .ReplaceLineEndings("\n")
                .TrimEnd('\n')
                .Split('\n')
                .Select(ParseLine),
        ];
    }

    private static MarkdownLine ParseLine(string line)
    {
        var leading = line.Length - line.TrimStart(' ').Length;
        var text = line.Trim();

        if (text.Length == 0)
        {
            return new MarkdownLine(0, IsBullet: false, IsHeading: false, []);
        }

        var isBullet = text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal);

        if (isBullet)
        {
            text = text[2..];
        }

        var isHeading = !isBullet && text[0] == Heading;

        if (isHeading)
        {
            text = text.TrimStart(Heading).TrimStart();
        }

        var spans = ParseSpans(text);

        isHeading |= !isBullet && spans.Count == 1 && spans[0].IsBold;

        return new MarkdownLine(leading / SpacesPerIndent, isBullet, isHeading, spans);
    }

    /// <summary>
    /// Walks the text once, toggling bold at each double asterisk and code at each backtick. Inside
    /// code nothing else is markup, which is what keeps a commit subject such as <c>a**b</c> intact.
    /// An unclosed marker simply leaves the rest of the line in that style.
    /// </summary>
    private static List<MarkdownSpan> ParseSpans(string text)
    {
        var spans = new List<MarkdownSpan>();
        var current = new StringBuilder();
        var bold = false;
        var code = false;

        void Flush()
        {
            if (current.Length > 0)
            {
                spans.Add(new MarkdownSpan(current.ToString(), bold, code));
                current.Clear();
            }
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == Code)
            {
                Flush();
                code = !code;
                continue;
            }

            if (!code && string.CompareOrdinal(text, index, Bold, 0, Bold.Length) == 0)
            {
                Flush();
                bold = !bold;
                index++;
                continue;
            }

            current.Append(text[index]);
        }

        Flush();

        return spans;
    }
}
