namespace SessionFinder.Presentation.Recap;

/// <summary>
/// One line of the recap, ready for a window to draw: how deep it is indented, whether it is a
/// bullet or a heading, and its text as plain, bold and code spans.
/// </summary>
/// <param name="Indent">The nesting depth, zero for a top-level line.</param>
/// <param name="IsBullet">Whether the line is a list item.</param>
/// <param name="IsHeading">Whether the line is a heading.</param>
/// <param name="Spans">The text, split where its style changes. Empty for a blank line.</param>
public sealed record MarkdownLine(int Indent, bool IsBullet, bool IsHeading, IReadOnlyList<MarkdownSpan> Spans)
{
    /// <summary>Whether the line has no text, which a window draws as vertical space.</summary>
    public bool IsBlank => Spans.Count == 0;

    /// <summary>
    /// What the line is in the recap's layout, which decides how a window styles it.
    /// </summary>
    public RecapLineRole Role
    {
        get
        {
            if (IsBlank)
            {
                return RecapLineRole.Blank;
            }

            var startsBold = Spans[0].IsBold;
            var onlyBold = Spans.Count == 1 && startsBold;

            if (IsHeading)
            {
                return string.Equals(Spans[0].Text.Trim(), SectionTitle, StringComparison.OrdinalIgnoreCase)
                    ? RecapLineRole.Section
                    : RecapLineRole.Heading;
            }

            if (!IsBullet)
            {
                return RecapLineRole.Text;
            }

            if (Indent == 0 && onlyBold)
            {
                return RecapLineRole.Heading;
            }

            return Indent == 0 && startsBold ? RecapLineRole.Project : RecapLineRole.Item;
        }
    }

    private const string SectionTitle = "Earlier";
}

/// <summary>
/// The parts a recap is laid out from.
/// </summary>
public enum RecapLineRole
{
    /// <summary>Vertical space between blocks.</summary>
    Blank,

    /// <summary>A divider that starts a section, such as the earlier days.</summary>
    Section,

    /// <summary>A day, or a heading of the summary.</summary>
    Heading,

    /// <summary>A project line: its name in bold, then branch, session count and time span.</summary>
    Project,

    /// <summary>A session, a commit or a summary bullet.</summary>
    Item,

    /// <summary>Any other line, shown as it is.</summary>
    Text,
}

/// <summary>
/// A run of text in one style.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="IsBold">Whether it was written between double asterisks.</param>
/// <param name="IsCode">Whether it was written between backticks.</param>
public sealed record MarkdownSpan(string Text, bool IsBold, bool IsCode);
