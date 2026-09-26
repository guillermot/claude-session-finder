using System.Drawing;
using SessionFinder.Presentation.Abstractions;
using WinForms = System.Windows.Forms;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// The notification-area icon, its menu and the hover text, on top of the Windows Forms
/// <c>NotifyIcon</c>.
/// </summary>
/// <remarks>
/// <para>
/// WPF has no notification-area icon of its own, and the alternative to borrowing this one is
/// calling <c>Shell_NotifyIcon</c> and owning the icon handle, the menu and the balloon lifetime by
/// hand. Borrowing costs one reference to a library already in the runtime.
/// </para>
/// <para>
/// A notification is put into the hover text as well as into a balloon, and that is not belt and
/// braces. Balloons are suppressed outright on machines where notifications are turned off for the
/// application, by focus assist, or by policy — measured on the machine this was built on, where a
/// balloon from a bare control produced nothing at all — so a notifier that only raised balloons
/// would be a notifier that says nothing. The hover text always works, and the log always works;
/// the balloon is the part that is allowed to fail.
/// </para>
/// </remarks>
internal sealed class NotifyIconTray : ITrayIcon, IUserNotifier, IDisposable
{
    private const string SearchMenuText = "Search…";
    private const string RebuildIndexMenuText = "Rebuild index";
    private const string SettingsMenuText = "Open settings";
    private const string LogFolderMenuText = "Open log folder";
    private const string ExitMenuText = "Exit";
    private const string StatusSeparator = " — ";
    private const string IconResourceName = "SessionFinder.Wpf.Assets.app.ico";
    private const int TooltipMaxLength = 63;
    private const int BalloonMilliseconds = 10_000;

    private readonly Icon _image;
    private readonly WinForms.NotifyIcon _icon;

    private string _baseTooltip = string.Empty;
    private string? _status;
    private bool _isDisposed;

    /// <summary>Builds the icon and its menu, without showing either.</summary>
    public NotifyIconTray()
    {
        var menu = new WinForms.ContextMenuStrip();

        menu.Items.Add(SearchMenuText, image: null, (_, _) => SearchRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(RebuildIndexMenuText, image: null, (_, _) => RebuildIndexRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(SettingsMenuText, image: null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(LogFolderMenuText, image: null, (_, _) => LogFolderRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(ExitMenuText, image: null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _image = LoadImage();

        _icon = new WinForms.NotifyIcon
        {
            Icon = _image,
            ContextMenuStrip = menu,
            Visible = false,
        };

        _icon.DoubleClick += (_, _) => SearchRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? SearchRequested;

    /// <inheritdoc />
    public event EventHandler? RebuildIndexRequested;

    /// <inheritdoc />
    public event EventHandler? SettingsRequested;

    /// <inheritdoc />
    public event EventHandler? LogFolderRequested;

    /// <inheritdoc />
    public event EventHandler? ExitRequested;

    /// <inheritdoc />
    public void Show() => _icon.Visible = true;

    /// <inheritdoc />
    public void SetTooltip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _baseTooltip = text;

        RefreshTooltip();
    }

    /// <inheritdoc />
    public void Notify(UserNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _icon.Visible = true;
        _status = notification.Title;

        RefreshTooltip();

        _icon.ShowBalloonTip(
            BalloonMilliseconds,
            notification.Title,
            notification.Message,
            ToBalloonIcon(notification.Severity));
    }

    /// <summary>Takes the icon out of the notification area and releases it.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
        _isDisposed = true;
    }

    /// <summary>
    /// Reads the icon out of the assembly, at the size the notification area draws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is an embedded resource rather than a file next to the executable because the application
    /// is published as a single self-contained file, where there is no "next to the executable":
    /// the process runs from an extraction directory whose contents are the runtime, not the assets.
    /// The bytes have to travel inside the assembly, and reading them from the manifest is the one
    /// way that holds whether the build is single-file, framework-dependent or a loose debug build.
    /// </para>
    /// <para>
    /// The small-icon size is asked for by name rather than left to the default. Without it the
    /// 32-pixel frame is chosen and squeezed into a 16-pixel slot, which throws away the layer of
    /// the icon that was drawn for that slot; with it, a scaled desktop gets the 20 or 24 frame
    /// instead of a blurred large one.
    /// </para>
    /// </remarks>
    private static Icon LoadImage()
    {
        using var stream = typeof(NotifyIconTray).Assembly.GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"The icon resource '{IconResourceName}' is missing from the assembly.");

        return new Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    /// <summary>
    /// Keeps the chord and the most recent message in the one line the shell gives us, with the
    /// chord first: it is the thing the tooltip exists to answer, and the message is what the log
    /// has in full.
    /// </summary>
    private void RefreshTooltip() =>
        _icon.Text = Fit(_status is null ? _baseTooltip : _baseTooltip + StatusSeparator + _status);

    /// <summary>
    /// The shell truncates a tooltip past its limit without saying so, which would silently cut off
    /// the chord — the one piece of information the tooltip exists to carry.
    /// </summary>
    private static string Fit(string text) =>
        text.Length <= TooltipMaxLength ? text : text[..TooltipMaxLength];

    private static WinForms.ToolTipIcon ToBalloonIcon(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Warning => WinForms.ToolTipIcon.Warning,
        NotificationSeverity.Error => WinForms.ToolTipIcon.Error,
        _ => WinForms.ToolTipIcon.Info,
    };
}
