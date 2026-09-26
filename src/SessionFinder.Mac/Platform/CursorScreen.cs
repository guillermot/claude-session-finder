using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Finds the screen the pointer is on.
/// </summary>
/// <remarks>
/// <para>
/// A launcher opens where the user is looking, and on a multi-monitor desk the pointer is the only
/// evidence of that available at the moment a chord is pressed: the window has not been shown yet,
/// so there is nothing to ask which screen it is on, and the frontmost application may be on a
/// different screen from the one being looked at.
/// </para>
/// <para>
/// Avalonia has no API for the pointer position outside of an input event, so this goes to Core
/// Graphics for it. Creating a null event and reading its location is the documented way to ask,
/// and it needs no permission because it reports where the cursor is rather than what is being
/// typed. The coordinates come back with the origin at the top left, which is the same convention
/// Avalonia's screen bounds use, so the two can be compared without flipping anything.
/// </para>
/// </remarks>
internal static partial class CursorScreen
{
    private const string CoreGraphicsFramework =
        "/System/Library/Frameworks/CoreGraphics.framework/Versions/Current/CoreGraphics";

    /// <summary>
    /// Picks the screen the pointer is on.
    /// </summary>
    /// <param name="screens">The screens Avalonia knows about.</param>
    /// <returns>
    /// The screen containing the pointer, or the primary screen when the pointer cannot be located
    /// or falls in the gap between two screens of different heights.
    /// </returns>
    public static Screen? ForPointer(Screens? screens)
    {
        if (screens is null)
        {
            return null;
        }

        if (TryGetPointerPosition(out var position))
        {
            foreach (var screen in screens.All)
            {
                if (screen.Bounds.Contains(position))
                {
                    return screen;
                }
            }
        }

        return screens.Primary ?? screens.All.FirstOrDefault();
    }

    private static bool TryGetPointerPosition(out PixelPoint position)
    {
        position = default;

        var eventRef = CGEventCreate(IntPtr.Zero);

        if (eventRef == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var location = CGEventGetLocation(eventRef);

            position = new PixelPoint((int)location.X, (int)location.Y);

            return true;
        }
        finally
        {
            CFRelease(eventRef);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [LibraryImport(CoreGraphicsFramework)]
    private static partial IntPtr CGEventCreate(IntPtr source);

    [LibraryImport(CoreGraphicsFramework)]
    private static partial CGPoint CGEventGetLocation(IntPtr eventRef);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/Versions/Current/CoreFoundation")]
    private static partial void CFRelease(IntPtr reference);
}
