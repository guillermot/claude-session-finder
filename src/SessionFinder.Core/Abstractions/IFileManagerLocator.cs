using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Finds the file manager a folder is shown in.
/// </summary>
/// <remarks>
/// <para>
/// This port exists later than the others and for a narrower reason. The file manager is part of
/// the operating system rather than something that may or may not be installed, so while there was
/// one head the name could sit as a constant inside the slice, and it did. A second head ended
/// that: two file managers now have to coexist in one solution, and the name is no longer
/// something a single line can be changed to.
/// </para>
/// <para>
/// Nothing probes the disk behind this the way the editor locator does. An implementation is
/// expected to be a constant with a return type, and that is fine — what the seam buys is two
/// constants rather than one, not a search.
/// </para>
/// </remarks>
public interface IFileManagerLocator
{
    /// <summary>
    /// Names the file manager on this machine.
    /// </summary>
    /// <returns>The file manager, or the reason none could be named.</returns>
    Result<LaunchProgram> Locate();
}
