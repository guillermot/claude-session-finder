using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// Grants or refuses chords according to a rule the test supplies, which is how "another
/// application already owns that one" is reproduced without another application.
/// </summary>
internal sealed class FakeGlobalHotkey(Func<HotkeyChord, bool> isAvailable) : IGlobalHotkey
{
    private readonly List<HotkeyChord> _attempts = [];

    public event EventHandler? Pressed;

    public IReadOnlyList<HotkeyChord> Attempts => _attempts;

    public HotkeyChord? Registered { get; private set; }

    public int UnregisterCount { get; private set; }

    public static FakeGlobalHotkey GrantingEverything() => new(_ => true);

    public static FakeGlobalHotkey GrantingNothing() => new(_ => false);

    public bool TryRegister(HotkeyChord chord)
    {
        _attempts.Add(chord);

        if (!isAvailable(chord))
        {
            return false;
        }

        Registered = chord;
        return true;
    }

    public void Unregister()
    {
        UnregisterCount++;
        Registered = null;
    }

    public void RaisePressed() => Pressed?.Invoke(this, EventArgs.Empty);
}
