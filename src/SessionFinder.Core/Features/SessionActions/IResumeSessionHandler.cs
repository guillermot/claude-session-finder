using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Reopens a session in a terminal.
/// </summary>
public interface IResumeSessionHandler
{
    /// <summary>
    /// Resumes the session.
    /// </summary>
    /// <param name="command">Which session to resume, and where it lives.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the terminal has been started, or why it was not.</returns>
    Task<Result> HandleAsync(ResumeSessionCommand command, CancellationToken cancellationToken);
}
