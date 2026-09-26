using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// The menu-bar item and its menu, which is the only part of the application that is always
/// reachable.
/// </summary>
/// <remarks>
/// <para>
/// The first entry is a disabled line naming the chord that was actually registered. On Windows
/// that belongs in the icon's hover text, but a menu-bar item has no hover text worth relying on,
/// and this is not decoration: when the chord could not be claimed — or was claimed and is being
/// swallowed by the system, which Carbon cannot report — this line is the only place the user can
/// find out. The menu underneath it is then the whole user interface.
/// </para>
/// <para>
/// The icon is read from an embedded resource rather than from a file beside the executable,
/// because inside a bundle the executable's neighbours are not where resources live.
/// </para>
/// <para>
/// The icon is registered on the application rather than left standing alone. On macOS a
/// <see cref="TrayIcon"/> that is only constructed never reaches the menu bar: the status item is
/// created when the icon joins the application's <c>TrayIcon.Icons</c>, which is also what has
/// Avalonia take it down when the run loop ends.
/// </para>
/// </remarks>
internal sealed class StatusItemTray : ITrayIcon, IDisposable
{
    private const string IconResourceName = "SessionFinder.Mac.Assets.tray.png";
    private const string ApplicationName = "Claude Session Finder";

    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _registeredChord;
    private bool _disposed;

    public StatusItemTray()
    {
        _registeredChord = new NativeMenuItem(ApplicationName) { IsEnabled = false };

        _icon = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = ApplicationName,
            IsVisible = false,
            Menu = BuildMenu(),
        };
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
    public void Show()
    {
        if (Application.Current is { } application && TrayIcon.GetIcons(application) is null)
        {
            TrayIcon.SetIcons(application, [_icon]);
        }

        _icon.IsVisible = true;
    }

    /// <inheritdoc />
    public void SetTooltip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _icon.ToolTipText = text;
        _registeredChord.Header = text;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _icon.IsVisible = false;
        _icon.Dispose();
        _disposed = true;
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu
        {
            Items =
            {
                _registeredChord,
                new NativeMenuItemSeparator(),
                Item("Search…", () => SearchRequested),
                Item("Rebuild index", () => RebuildIndexRequested),
                Item("Open settings", () => SettingsRequested),
                Item("Open log folder", () => LogFolderRequested),
                new NativeMenuItemSeparator(),
                Item("Quit", () => ExitRequested),
            },
        };

        return menu;
    }

    /// <summary>
    /// Builds one menu entry. The handler is read through a function rather than captured, because
    /// the events are declared after the menu is built and a captured null would stay null.
    /// </summary>
    private NativeMenuItem Item(string header, Func<EventHandler?> handler)
    {
        var item = new NativeMenuItem(header);

        item.Click += (_, _) => handler()?.Invoke(this, EventArgs.Empty);

        return item;
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException(
                $"The menu-bar icon {IconResourceName} is missing from the assembly.");

        return new WindowIcon(new Bitmap(stream));
    }
}
