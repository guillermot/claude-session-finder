using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Shows a macOS notification.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is not guaranteed and is not treated as though it were. macOS shows a notification only
/// for an application it recognises, and an ad-hoc-signed bundle — which is what this is until
/// somebody pays for a certificate — can be dropped without a word. That is the same situation the
/// Windows head is in with suppressed balloons, and it has the same answer: this is wrapped in
/// <c>LoggingUserNotifier</c>, so the log has every notification whether or not the screen did.
/// </para>
/// <para>
/// Failing to notify is therefore swallowed rather than reported. There is nowhere to report it to
/// that is not the thing that just failed.
/// </para>
/// </remarks>
/// <param name="logger">Where a notification that could not be shown is recorded.</param>
internal sealed class OsaScriptNotifier(ILogger<OsaScriptNotifier> logger) : IUserNotifier
{
    private const string OsaScriptExecutable = "/usr/bin/osascript";
    private const string ScriptLineFlag = "-e";

    /// <inheritdoc />
    public void Notify(UserNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var script = "display notification "
            + $"\"{Escape(notification.Message)}\" with title \"{Escape(notification.Title)}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = OsaScriptExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(ScriptLineFlag);
        startInfo.ArgumentList.Add(script);

        try
        {
            Process.Start(startInfo)?.Dispose();
        }
        catch (Win32Exception exception)
        {
            MacNotificationLog.NotShown(logger, exception);
        }
        catch (InvalidOperationException exception)
        {
            MacNotificationLog.NotShown(logger, exception);
        }
    }

    /// <summary>
    /// Escapes text for the inside of an AppleScript string literal, backslashes before quotes so
    /// that the quotes this adds are not themselves doubled on the way past.
    /// </summary>
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
