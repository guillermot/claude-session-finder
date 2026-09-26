namespace SessionFinder.Core.Configuration;

/// <summary>
/// How the search shell behaves: which chord summons it, how long it waits before turning a
/// keystroke into a query, and whether losing focus is enough to dismiss it.
/// </summary>
/// <remarks>
/// These are head-agnostic on purpose. A chord is a string here rather than a platform key code,
/// and the window behaviour is expressed as intent rather than as a window style, so the same
/// settings file drives a Windows head and, later, one that is not.
/// </remarks>
public sealed class ShellOptions
{
    /// <summary>Configuration section the options are bound from.</summary>
    public const string SectionName = "Finder:Shell";

    /// <summary>The chord tried first, before the built-in fallbacks.</summary>
    public const string DefaultHotkey = "Ctrl+Alt+Space";

    /// <summary>How long typing has to pause before a query is run.</summary>
    public const int DefaultDebounceMilliseconds = 150;

    /// <summary>
    /// The chord that summons the search box, written as <c>Ctrl+Alt+Space</c>. Blank uses
    /// <see cref="DefaultHotkey"/>. An unregistrable chord is not an error: the shell walks its
    /// fallback chain and reports which chord it actually got.
    /// </summary>
    public string? Hotkey { get; set; }

    /// <summary>
    /// How long typing has to pause before a query is run. Comfortably above the measured
    /// as-you-type latency of the index, so the wait is the user's pause rather than the query.
    /// </summary>
    public int DebounceMilliseconds { get; set; } = DefaultDebounceMilliseconds;

    /// <summary>
    /// Whether the search box hides as soon as it loses focus. On for everyday use, where clicking
    /// away is how a launcher is dismissed; off while debugging, because attaching a debugger or
    /// opening a log window deactivates the window and takes the thing being inspected with it.
    /// </summary>
    public bool HideOnDeactivate { get; set; } = true;

    /// <summary>
    /// Whether the operating system starts the launcher when the user signs in.
    /// </summary>
    /// <remarks>
    /// This value, and not the operating system's own registration, is what the application
    /// believes. The registration is rewritten to agree with it at every start, so a settings file
    /// copied to another machine brings the behaviour with it and a registration removed by some
    /// other tool comes back.
    /// </remarks>
    public bool StartAtLogin { get; set; }

    /// <summary>
    /// Absolute path of the editor executable. Blank makes the head search the places the editor is
    /// normally installed, which is the case that needs no settings file at all; this exists for the
    /// portable and non-standard installations that search cannot be expected to find.
    /// </summary>
    public string? EditorPath { get; set; }

    /// <summary>
    /// Absolute path of the terminal a resumed session opens in. Blank makes the head prefer the
    /// tabbed terminal host and fall back to the command shell.
    /// </summary>
    public string? TerminalPath { get; set; }
}
