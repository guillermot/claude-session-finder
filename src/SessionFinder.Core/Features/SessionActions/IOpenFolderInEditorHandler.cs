using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Opens a session's folder in the editor.
/// </summary>
public interface IOpenFolderInEditorHandler
{
    /// <summary>
    /// Opens the folder.
    /// </summary>
    /// <param name="command">Which folder to open.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the editor has been started, or why it was not.</returns>
    Task<Result> HandleAsync(OpenFolderInEditorCommand command, CancellationToken cancellationToken);
}
