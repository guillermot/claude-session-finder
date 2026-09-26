using System.Globalization;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// A system-wide key combination, written the way a user writes one: modifiers, then a key.
/// </summary>
/// <remarks>
/// <para>
/// The key is a name rather than a platform key code because this type is read from a settings file
/// and shown in a tooltip, and neither of those is a place for a number. Turning the name into
/// something the operating system understands is the adapter's job, and an unrecognised name is
/// reported as a failed registration rather than as an exception — which is exactly how a chord
/// another application already owns is reported, so both end up walking the same fallback chain.
/// </para>
/// <para>
/// The default is Control, Alt and Space. The obvious alternatives are taken: the Windows key with
/// Space switches input methods, and Alt with Space opens the system menu of the focused window.
/// </para>
/// </remarks>
/// <param name="Modifiers">The modifiers that must be held.</param>
/// <param name="KeyName">The name of the key, such as <c>Space</c>, <c>K</c> or <c>F12</c>.</param>
public sealed record HotkeyChord(HotkeyModifiers Modifiers, string KeyName)
{
    private const char PartSeparator = '+';
    private const string ControlName = "Ctrl";
    private const string AltName = "Alt";
    private const string ShiftName = "Shift";
    private const string WindowsName = "Win";

    /// <summary>The chord the application asks for first.</summary>
    public static HotkeyChord Default { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, "Space");

    /// <summary>
    /// The chords to try, in order, when nothing has been configured. Each one is progressively
    /// less likely to be owned by something else and progressively less comfortable to press, which
    /// is the right trade in that order.
    /// </summary>
    public static IReadOnlyList<HotkeyChord> FallbackChain { get; } =
    [
        Default,
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, "K"),
        new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "F12"),
    ];

    /// <summary>
    /// Reads a chord written as <c>Ctrl+Alt+Space</c>.
    /// </summary>
    /// <param name="text">The chord text; case and spacing around the separators do not matter.</param>
    /// <param name="chord">The parsed chord, or <see langword="null"/> on failure.</param>
    /// <returns>
    /// <see langword="true"/> when the text names at least one modifier and exactly one other key.
    /// A chord without a modifier is rejected: registering a bare key system-wide would swallow it
    /// from every other application.
    /// </returns>
    public static bool TryParse(string? text, out HotkeyChord? chord)
    {
        chord = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        string? keyName = null;

        foreach (var part in text.Split(PartSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ReadModifier(part) is { } modifier)
            {
                modifiers |= modifier;
                continue;
            }

            if (keyName is not null)
            {
                return false;
            }

            keyName = NormalizeKeyName(part);
        }

        if (modifiers == HotkeyModifiers.None || keyName is null)
        {
            return false;
        }

        chord = new HotkeyChord(modifiers, keyName);
        return true;
    }

    /// <summary>
    /// Builds the ordered list of chords to try for a configured preference.
    /// </summary>
    /// <param name="configured">The configured chord text, which may be blank or unparseable.</param>
    /// <returns>
    /// The configured chord first, followed by the built-in fallbacks minus any duplicate of it.
    /// An unparseable preference simply leaves the built-in chain, because a typo in a settings
    /// file should cost the user their preferred chord and not their hotkey.
    /// </returns>
    public static IReadOnlyList<HotkeyChord> ChainFor(string? configured)
    {
        if (!TryParse(configured, out var preferred) || preferred is null)
        {
            return FallbackChain;
        }

        return [preferred, .. FallbackChain.Where(chord => chord != preferred)];
    }

    /// <summary>Renders the chord the way it is written in settings and shown in the tooltip.</summary>
    /// <returns>The chord text, such as <c>Ctrl+Alt+Space</c>.</returns>
    public override string ToString()
    {
        var parts = new List<string>(capacity: 4);

        AppendIfSet(parts, HotkeyModifiers.Control, ControlName);
        AppendIfSet(parts, HotkeyModifiers.Alt, AltName);
        AppendIfSet(parts, HotkeyModifiers.Shift, ShiftName);
        AppendIfSet(parts, HotkeyModifiers.Windows, WindowsName);
        parts.Add(KeyName);

        return string.Join(PartSeparator, parts);
    }

    private void AppendIfSet(List<string> parts, HotkeyModifiers modifier, string name)
    {
        if (Modifiers.HasFlag(modifier))
        {
            parts.Add(name);
        }
    }

    private static HotkeyModifiers? ReadModifier(string part) => part.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" or "OPT" or "OPTION" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" or "META" or "CMD" => HotkeyModifiers.Windows,
        _ => null,
    };

    /// <summary>
    /// Settles the key name on one spelling so that two chords written differently compare equal
    /// and the tooltip does not echo whatever casing the settings file happened to use.
    /// </summary>
    private static string NormalizeKeyName(string part) => string.Concat(
        char.ToUpperInvariant(part[0]).ToString(CultureInfo.InvariantCulture),
        part[1..].ToLowerInvariant());
}
