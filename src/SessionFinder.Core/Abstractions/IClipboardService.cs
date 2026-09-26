using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Puts text on the system clipboard.
/// </summary>
/// <remarks>
/// The operation is asynchronous and can fail, which looks like overreach for "copy a string" and
/// is not: the clipboard is a single system-wide resource that any other running program may be
/// holding at the moment the key is pressed, and an implementation that retries has to be able to
/// wait and to give up.
/// </remarks>
public interface IClipboardService
{
    /// <summary>
    /// Replaces the clipboard contents with the given text.
    /// </summary>
    /// <param name="text">What to put on the clipboard.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the text is on the clipboard, or why it could not be put there.</returns>
    Task<Result> SetTextAsync(string text, CancellationToken cancellationToken);
}
