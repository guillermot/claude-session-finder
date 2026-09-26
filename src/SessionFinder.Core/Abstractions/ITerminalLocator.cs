using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Finds the terminal a resumed session is shown in.
/// </summary>
public interface ITerminalLocator
{
    /// <summary>
    /// Looks for a terminal on this machine.
    /// </summary>
    /// <returns>The terminal, or the reason none could be found.</returns>
    Result<TerminalProgram> Locate();
}
