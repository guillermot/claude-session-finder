namespace SessionFinder.Presentation.Shell;

/// <summary>
/// The modifier keys a chord can require.
/// </summary>
/// <remarks>
/// The names are the ones a Windows keyboard is labelled with because that is the head that came
/// first and the settings file has to keep spelling them the same way. Each adapter reads them for
/// its own keyboard: on macOS <see cref="Alt"/> is Option and <see cref="Windows"/> is Command, and
/// <c>HotkeyChord</c> accepts both spellings on the way in.
/// </remarks>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>No modifier, which is never a valid chord for a system-wide hotkey.</summary>
    None = 0,

    /// <summary>Either Alt key, which is either Option key on macOS.</summary>
    Alt = 1,

    /// <summary>Either Control key.</summary>
    Control = 2,

    /// <summary>Either Shift key.</summary>
    Shift = 4,

    /// <summary>Either Windows key, which is either Command key on macOS.</summary>
    Windows = 8,
}
