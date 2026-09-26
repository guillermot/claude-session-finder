using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// A system-wide key combination the application listens for while another application has focus.
/// </summary>
/// <remarks>
/// Registration returns a boolean rather than throwing, because a chord already owned by another
/// application is an ordinary fact about the machine and not a fault. The policy for what to try
/// next belongs to <see cref="ShellCoordinator"/>, which is why this port knows about one chord at
/// a time and nothing about the chain.
/// </remarks>
public interface IGlobalHotkey
{
    /// <summary>Raised on the user interface thread when the registered chord is pressed.</summary>
    event EventHandler? Pressed;

    /// <summary>
    /// Claims a chord system-wide, replacing any chord this instance already holds.
    /// </summary>
    /// <param name="chord">The combination to claim.</param>
    /// <returns>
    /// <see langword="true"/> when the chord was claimed; <see langword="false"/> when another
    /// application already holds it or the key name is not one this platform recognises.
    /// </returns>
    bool TryRegister(HotkeyChord chord);

    /// <summary>Releases the chord, if one is held.</summary>
    void Unregister();
}
