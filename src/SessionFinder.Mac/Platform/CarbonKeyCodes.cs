using System.Globalization;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Turns the name of a key into the virtual key code Carbon registers a hotkey with.
/// </summary>
/// <remarks>
/// <para>
/// The codes are the <c>kVK_</c> constants from <c>HIToolbox/Events.h</c>. They describe a physical
/// position on the keyboard rather than the character it produces, which is what makes them the
/// right thing for a chord: the key next to the left shift is the same key whatever layout is
/// active, and a hotkey that moved when the user switched to a Spanish layout would be a bug.
/// </para>
/// <para>
/// Only the keys a person would put in a chord are here. A name that is missing is reported as an
/// unregistrable chord, which sends the shell down its fallback chain — the same outcome as a chord
/// another application already owns, and for the user the same thing has happened either way.
/// </para>
/// </remarks>
internal static class CarbonKeyCodes
{
    private static readonly Dictionary<string, uint> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = 0x00, ["S"] = 0x01, ["D"] = 0x02, ["F"] = 0x03, ["H"] = 0x04, ["G"] = 0x05,
        ["Z"] = 0x06, ["X"] = 0x07, ["C"] = 0x08, ["V"] = 0x09, ["B"] = 0x0B, ["Q"] = 0x0C,
        ["W"] = 0x0D, ["E"] = 0x0E, ["R"] = 0x0F, ["Y"] = 0x10, ["T"] = 0x11, ["O"] = 0x1F,
        ["U"] = 0x20, ["I"] = 0x22, ["P"] = 0x23, ["L"] = 0x25, ["J"] = 0x26, ["K"] = 0x28,
        ["N"] = 0x2D, ["M"] = 0x2E,

        ["1"] = 0x12, ["2"] = 0x13, ["3"] = 0x14, ["4"] = 0x15, ["6"] = 0x16, ["5"] = 0x17,
        ["9"] = 0x19, ["7"] = 0x1A, ["8"] = 0x1C, ["0"] = 0x1D,

        ["Equals"] = 0x18, ["Minus"] = 0x1B, ["OemCloseBrackets"] = 0x1E,
        ["OemOpenBrackets"] = 0x21, ["OemQuotes"] = 0x27, ["OemSemicolon"] = 0x29,
        ["OemBackslash"] = 0x2A, ["OemComma"] = 0x2B, ["OemQuestion"] = 0x2C, ["OemPeriod"] = 0x2F,
        ["OemTilde"] = 0x32,

        ["Return"] = 0x24, ["Enter"] = 0x24, ["Tab"] = 0x30, ["Space"] = 0x31,
        ["Back"] = 0x33, ["Backspace"] = 0x33, ["Escape"] = 0x35, ["Esc"] = 0x35,
        ["Delete"] = 0x75, ["Home"] = 0x73, ["End"] = 0x77,
        ["PageUp"] = 0x74, ["PageDown"] = 0x79,
        ["Left"] = 0x7B, ["Right"] = 0x7C, ["Down"] = 0x7D, ["Up"] = 0x7E,

        ["F1"] = 0x7A, ["F2"] = 0x78, ["F3"] = 0x63, ["F4"] = 0x76, ["F5"] = 0x60,
        ["F6"] = 0x61, ["F7"] = 0x62, ["F8"] = 0x64, ["F9"] = 0x65, ["F10"] = 0x6D,
        ["F11"] = 0x67, ["F12"] = 0x6F, ["F13"] = 0x69, ["F14"] = 0x6B, ["F15"] = 0x71,
    };

    /// <summary>
    /// Looks up the virtual key code for a key name.
    /// </summary>
    /// <param name="keyName">The name as it is written in a chord, such as <c>Space</c>.</param>
    /// <param name="keyCode">The code, when the name is one this platform knows.</param>
    /// <returns><see langword="true"/> when the name was recognised.</returns>
    public static bool TryResolve(string keyName, out uint keyCode)
    {
        keyCode = 0;

        return !string.IsNullOrWhiteSpace(keyName)
            && ByName.TryGetValue(keyName.Trim(), out keyCode);
    }

    /// <summary>
    /// Renders a chord with the symbols a Mac keyboard is labelled with, which is how every other
    /// application on the machine writes one and therefore the only spelling a user can compare
    /// against what they pressed.
    /// </summary>
    /// <param name="chord">The chord to render.</param>
    /// <returns>The chord written in modifier symbols, such as <c>⌥Space</c>.</returns>
    public static string ToDisplayString(Presentation.Shell.HotkeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);

        var symbols = string.Concat(
            chord.Modifiers.HasFlag(Presentation.Shell.HotkeyModifiers.Control) ? "⌃" : string.Empty,
            chord.Modifiers.HasFlag(Presentation.Shell.HotkeyModifiers.Alt) ? "⌥" : string.Empty,
            chord.Modifiers.HasFlag(Presentation.Shell.HotkeyModifiers.Shift) ? "⇧" : string.Empty,
            chord.Modifiers.HasFlag(Presentation.Shell.HotkeyModifiers.Windows) ? "⌘" : string.Empty);

        return string.Create(CultureInfo.InvariantCulture, $"{symbols}{chord.KeyName}");
    }
}
