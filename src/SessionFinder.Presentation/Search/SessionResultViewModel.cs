using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Search;

namespace SessionFinder.Presentation.Search;

/// <summary>
/// One row of the result list, with every value already in the form the row displays.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here changes after the row is built, so there is no change notification and no observable
/// property. A new search replaces the rows rather than mutating them, which is both simpler and
/// what the list actually does.
/// </para>
/// <para>
/// The three values the user asked for — title, last activity and folder — are always populated:
/// an unknown folder is a sentence saying so rather than a blank, because a blank in a result list
/// reads as a rendering fault.
/// </para>
/// </remarks>
public sealed record SessionResultViewModel
{
    private const string UnknownFolderText = "folder unknown";

    /// <summary>The session identifier, which is what a resume will need.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>Absolute path of the transcript the row came from.</summary>
    public required string FilePath { get; init; }

    /// <summary>The resolved title, which is the first thing the row shows.</summary>
    public required string Title { get; init; }

    /// <summary>Which candidate produced <see cref="Title"/>.</summary>
    public required TitleSource TitleSource { get; init; }

    /// <summary>When the session was last written to, in words.</summary>
    public required string LastActivity { get; init; }

    /// <summary>
    /// The folder the session was started from, kept as the value object rather than as text
    /// because every action on this row takes it as an argument and one of them will not work
    /// without the exact spelling.
    /// </summary>
    public required WorkingFolder WorkingFolder { get; init; }

    /// <summary>The folder the session was started from, in its original casing.</summary>
    public string Folder => WorkingFolder.IsKnown ? WorkingFolder.Display : UnknownFolderText;

    /// <summary>Whether a folder is known, which is what decides if folder-bound actions can run.</summary>
    public bool IsFolderKnown => WorkingFolder.IsKnown;

    /// <summary>The git branch recorded for the session, when there was one.</summary>
    public string? Branch { get; init; }

    /// <summary>A fragment of the matching text, on one line, when the query produced one.</summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// Builds a row from a ranked hit.
    /// </summary>
    /// <param name="hit">The hit to display.</param>
    /// <param name="now">The moment the last activity is described against.</param>
    /// <returns>The row.</returns>
    public static SessionResultViewModel From(SessionHit hit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hit);

        var session = hit.Session;

        return new SessionResultViewModel
        {
            SessionId = session.SessionId,
            FilePath = session.FilePath,
            Title = session.Title.Text,
            TitleSource = session.Title.Source,
            LastActivity = LastActivityText.For(session.LastActivity, now),
            WorkingFolder = session.Folder,
            Branch = session.GitBranch,
            Snippet = Flatten(session.Snippet),
        };
    }

    /// <summary>
    /// Collapses the line breaks a snippet inherits from a pasted prompt, so that one result stays
    /// one row however the text was originally laid out.
    /// </summary>
    private static string? Flatten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
