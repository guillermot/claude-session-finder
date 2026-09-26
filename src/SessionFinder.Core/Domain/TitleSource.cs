namespace SessionFinder.Core.Domain;

/// <summary>
/// Which candidate won when a session title was resolved, in precedence order. Stored alongside
/// the title so a later pass can tell an upgrade from a downgrade.
/// </summary>
public enum TitleSource
{
    /// <summary>No candidate at all; the title is a placeholder.</summary>
    Unknown = 0,

    /// <summary>A title the user typed, carried by a <c>custom-title</c> record.</summary>
    CustomTitle = 1,

    /// <summary>A generated title, carried by an <c>ai-title</c> record.</summary>
    AiTitle = 2,

    /// <summary>A truncated form of the first human prompt in the session.</summary>
    FirstPrompt = 3,

    /// <summary>The transcript file name, which is the session identifier.</summary>
    FileName = 4,
}
