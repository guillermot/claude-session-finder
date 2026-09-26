using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Asks for a session's folder to be shown in the system file manager.
/// </summary>
/// <param name="Folder">The folder the session was started in.</param>
public sealed record RevealInFileExplorerCommand(WorkingFolder Folder);
