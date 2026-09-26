using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Asks for one piece of a session to be put on the clipboard.
/// </summary>
/// <param name="Detail">Which piece to copy.</param>
/// <param name="SessionId">The session being copied from.</param>
/// <param name="Folder">The folder the session was started in, which may be unknown.</param>
public sealed record CopySessionDetailCommand(
    SessionDetail Detail,
    SessionId SessionId,
    WorkingFolder Folder);
