using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Asks for a session to be reopened in a terminal.
/// </summary>
/// <param name="SessionId">The session to resume.</param>
/// <param name="Folder">
/// The folder the session was started in. Not optional: the resume only resolves the identifier
/// when it runs from exactly that directory.
/// </param>
public sealed record ResumeSessionCommand(SessionId SessionId, WorkingFolder Folder);
