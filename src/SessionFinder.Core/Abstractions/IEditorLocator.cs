using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Finds the editor to open a folder in.
/// </summary>
/// <remarks>
/// Locating is its own port rather than a field on the launcher because "which editor" is answered
/// by searching the machine, and that search is the part that differs between operating systems.
/// What is done with the answer does not.
/// </remarks>
public interface IEditorLocator
{
    /// <summary>
    /// Looks for an editor on this machine.
    /// </summary>
    /// <returns>The editor, or the reason none could be found.</returns>
    Result<LaunchProgram> Locate();
}
