namespace SessionFinder.Core.Domain;

/// <summary>
/// What a searchable chunk of text is, which decides both its ranking weight and whether a later
/// pass is allowed to replace it.
/// </summary>
public enum ChunkKind
{
    /// <summary>Unclassified text. Never written by the parser.</summary>
    Unknown = 0,

    /// <summary>The resolved session title. Replaced whenever the title is re-resolved.</summary>
    Title = 1,

    /// <summary>The working folder and git branch, so a folder name is itself searchable.</summary>
    Folder = 2,

    /// <summary>The <c>lastPrompt</c> value the session records for its resume banner.</summary>
    LastPrompt = 3,

    /// <summary>The text of a human prompt.</summary>
    UserPrompt = 4,

    /// <summary>A text block from an assistant turn. Thinking and tool traffic are excluded.</summary>
    AssistantText = 5,
}
