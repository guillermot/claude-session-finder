using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Asks the desktop window manager to draw a window's native title bar in its dark colours.
/// </summary>
/// <remarks>
/// <para>
/// A settings form keeps the real chrome — the system menu, the minimise and maximise buttons and
/// the resize border all come free with it, and reimplementing them to change one colour is how a
/// dialog ends up with a close button that does not match the desktop. What is changed is the one
/// attribute that decides which palette the chrome is painted from, so the bar above a near-black
/// window stops being white.
/// </para>
/// <para>
/// The attribute was numbered 19 while it was undocumented and 20 from Windows 10 build 18985
/// onwards, and the older number is rejected on newer builds rather than ignored. Both are tried,
/// current number first. An operating system that knows neither — or has no desktop window manager
/// at all — leaves the light bar in place, which is the correct outcome for a cosmetic call: the
/// window is still a window, and nothing else about it depends on this.
/// </para>
/// </remarks>
internal static partial class ImmersiveDarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeBeforeBuild18985 = 19;
    private const int Enabled = 1;
    private const int Success = 0;

    /// <summary>
    /// Turns the dark title bar on for a window that already has a handle.
    /// </summary>
    /// <param name="window">The window whose chrome should be repainted. Must have been shown or
    /// have raised <see cref="Window.SourceInitialized"/>, because before that it has no handle.</param>
    /// <returns><see langword="true"/> when the window manager accepted the attribute;
    /// <see langword="false"/> when it is unavailable, in which case the chrome stays as it was.</returns>
    public static bool TryApply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;

        return handle != IntPtr.Zero && TrySetDarkMode(handle);
    }

    private static bool TrySetDarkMode(IntPtr handle)
    {
        try
        {
            return TrySetAttribute(handle, UseImmersiveDarkMode)
                || TrySetAttribute(handle, UseImmersiveDarkModeBeforeBuild18985);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool TrySetAttribute(IntPtr handle, int attribute)
    {
        var enabled = Enabled;

        return DwmSetWindowAttribute(handle, attribute, ref enabled, sizeof(int)) == Success;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
