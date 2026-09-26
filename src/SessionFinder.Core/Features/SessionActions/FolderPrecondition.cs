using SessionFinder.Core.Domain;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// The check every folder-dependent action starts with.
/// </summary>
/// <remarks>
/// Three of the four actions are meaningless without a folder, and a session whose transcript never
/// recorded one is a normal thing to find rather than a fault. Failing here, identically, in one
/// place, is what lets the result list render those actions as explained and disabled instead of
/// letting each handler invent its own way of refusing.
/// </remarks>
internal static class FolderPrecondition
{
    /// <summary>
    /// Confirms that a folder was recovered for the session.
    /// </summary>
    /// <param name="folder">The folder the session recorded.</param>
    /// <returns>Success when the folder is known, otherwise the folder-unknown failure.</returns>
    public static Result Check(WorkingFolder folder) =>
        folder.IsKnown ? Result.Success() : Result.Failure(AppError.FolderUnknown);
}
