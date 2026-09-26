using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Puts text on the macOS pasteboard.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia hangs the clipboard off a top-level window rather than off the application, so the
/// window has to be found at the moment of the copy instead of injected. That is not only a
/// workaround for an API shape: taking the window as a constructor dependency would close a cycle,
/// because the window is built from the view model that owns the copy commands that need this.
/// </para>
/// <para>
/// There is no retry loop here. The Windows implementation has one because that clipboard is a
/// single system-wide resource another process can hold open; the pasteboard is not exclusive in
/// the same way and has nothing to wait for.
/// </para>
/// </remarks>
internal sealed class AvaloniaClipboardService : IClipboardService
{
    private const string NoWindowReason = "the search window was not open";

    /// <inheritdoc />
    public async Task<Result> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        cancellationToken.ThrowIfCancellationRequested();

        return await Dispatcher.UIThread
            .InvokeAsync(() => CopyAsync(text))
            .ConfigureAwait(false);
    }

    private static async Task<Result> CopyAsync(string text)
    {
        if (Clipboard() is not { } clipboard)
        {
            return Result.Failure(AppError.ClipboardUnavailable(NoWindowReason));
        }

        try
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);

            return Result.Success();
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure(AppError.ClipboardUnavailable(exception.Message));
        }
    }

    private static IClipboard? Clipboard() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.Windows
            .FirstOrDefault()
            ?.Clipboard;
}
