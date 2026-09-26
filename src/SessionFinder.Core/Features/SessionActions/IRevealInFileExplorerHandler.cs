using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Shows a session's folder in the system file manager.
/// </summary>
public interface IRevealInFileExplorerHandler
{
    /// <summary>
    /// Shows the folder.
    /// </summary>
    /// <param name="command">Which folder to show.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the file manager has been started, or why it was not.</returns>
    Task<Result> HandleAsync(RevealInFileExplorerCommand command, CancellationToken cancellationToken);
}
