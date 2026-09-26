using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Puts one piece of a session on the clipboard.
/// </summary>
public interface ICopySessionDetailHandler
{
    /// <summary>
    /// Copies the requested detail.
    /// </summary>
    /// <param name="command">Which detail, of which session.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the text is on the clipboard, or why it is not.</returns>
    Task<Result> HandleAsync(CopySessionDetailCommand command, CancellationToken cancellationToken);
}
