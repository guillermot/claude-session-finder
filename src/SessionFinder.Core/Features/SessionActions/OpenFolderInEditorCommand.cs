using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Asks for a session's folder to be opened in the editor.
/// </summary>
/// <param name="Folder">The folder the session was started in.</param>
public sealed record OpenFolderInEditorCommand(WorkingFolder Folder);
