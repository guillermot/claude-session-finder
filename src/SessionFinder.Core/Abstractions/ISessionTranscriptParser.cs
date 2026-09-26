using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Turns transcript bytes into the indexable view of a session.
/// </summary>
public interface ISessionTranscriptParser
{
    /// <summary>
    /// Reads a transcript from its current position to the end.
    /// </summary>
    /// <param name="request">The stream to read and what is already known about the session.</param>
    /// <param name="cancellationToken">Checked periodically between lines.</param>
    /// <returns>
    /// The document produced by this pass, whose parse offset never moves past a line that is not
    /// newline-terminated.
    /// </returns>
    Task<SessionDocument> ParseAsync(TranscriptParseRequest request, CancellationToken cancellationToken);
}
