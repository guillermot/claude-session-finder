using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Puts text on the Windows clipboard.
/// </summary>
/// <remarks>
/// <para>
/// The clipboard is opened exclusively by whichever process is using it, so an attempt made while
/// another application is reading or writing fails outright rather than waiting its turn. Clipboard
/// managers, remote-desktop clients and editors all take it briefly and often, which makes a single
/// attempt unreliable in a way that has nothing to do with this application. Retrying over a short
/// window turns that into the non-event it should be.
/// </para>
/// <para>
/// The data object is placed with a copy rather than a reference, so that the text survives this
/// process exiting. Without it the clipboard holds a pointer into an application that the user is
/// very likely to dismiss with the Escape key a second later.
/// </para>
/// <para>
/// Every attempt runs on the thread that owns the windows, because the clipboard is reached through
/// single-threaded apartment COM and calling it from anywhere else fails for a reason that has
/// nothing to do with contention.
/// </para>
/// </remarks>
/// <param name="dispatcher">The dispatcher of the thread that owns the windows.</param>
internal sealed class WpfClipboardService(Dispatcher dispatcher) : IClipboardService
{
    private const int MaximumAttempts = 5;
    private const int BackoffMilliseconds = 50;

    /// <inheritdoc />
    public async Task<Result> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        Exception? lastFailure = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lastFailure = await dispatcher.InvokeAsync(() => TrySet(text)).Task.ConfigureAwait(false);

            if (lastFailure is null)
            {
                return Result.Success();
            }

            await Task
                .Delay(TimeSpan.FromMilliseconds(BackoffMilliseconds), cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Failure(AppError.ClipboardUnavailable(lastFailure!.Message));
    }

    /// <summary>
    /// Makes one attempt and reports the failure rather than throwing it, so the retry loop reads as
    /// a loop instead of as exception handling.
    /// </summary>
    private static Exception? TrySet(string text)
    {
        try
        {
            Clipboard.SetDataObject(text, copy: true);

            return null;
        }
        catch (COMException exception)
        {
            return exception;
        }
        catch (ExternalException exception)
        {
            return exception;
        }
    }
}
