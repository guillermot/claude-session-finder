using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Tests.Shell;

public sealed class HotkeyChordTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("ctrl + alt + space")]
    [InlineData("CONTROL+ALT+SPACE")]
    public void TryParse_TheDefaultChordHoweverItIsWritten_ProducesTheDefaultChord(string text)
    {
        var parsed = HotkeyChord.TryParse(text, out var chord);

        parsed.Should().BeTrue();
        chord.Should().Be(HotkeyChord.Default);
    }

    /// <summary>
    /// A chord written on a Mac keyboard names the keys that keyboard is labelled with. They are
    /// the same two modifiers: Option is Alt and Command is the Windows key, and the parser accepts
    /// either spelling so one settings file can be carried between the two machines.
    /// </summary>
    [Theory]
    [InlineData("Opt+Space")]
    [InlineData("Option+Space")]
    [InlineData("option + space")]
    public void TryParse_TheMacSpellingOfAlt_ProducesTheSameChordAsAlt(string text)
    {
        var parsed = HotkeyChord.TryParse(text, out var chord);

        parsed.Should().BeTrue();
        chord.Should().Be(new HotkeyChord(HotkeyModifiers.Alt, "Space"));
    }

    [Theory]
    [InlineData("Cmd+Space")]
    [InlineData("Win+Space")]
    [InlineData("Meta+Space")]
    public void TryParse_TheMacSpellingOfTheWindowsKey_ProducesTheSameChord(string text)
    {
        var parsed = HotkeyChord.TryParse(text, out var chord);

        parsed.Should().BeTrue();
        chord.Should().Be(new HotkeyChord(HotkeyModifiers.Windows, "Space"));
    }

    [Fact]
    public void TryParse_AChordWithNoModifier_Fails()
    {
        var parsed = HotkeyChord.TryParse("Space", out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void TryParse_TwoNonModifierKeys_Fails()
    {
        var parsed = HotkeyChord.TryParse("Ctrl+K+J", out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void TryParse_BlankText_Fails()
    {
        var parsed = HotkeyChord.TryParse("   ", out _);

        parsed.Should().BeFalse();
    }

    [Fact]
    public void ToString_AChordWithSeveralModifiers_WritesThemInTheOrderKeyboardsAreLabelled()
    {
        var chord = new HotkeyChord(
            HotkeyModifiers.Shift | HotkeyModifiers.Control | HotkeyModifiers.Alt,
            "F12");

        chord.ToString().Should().Be("Ctrl+Alt+Shift+F12");
    }

    [Fact]
    public void ChainFor_NoConfiguredChord_StartsWithTheDefault()
    {
        var chain = HotkeyChord.ChainFor(null);

        chain[0].Should().Be(HotkeyChord.Default);
    }

    [Fact]
    public void ChainFor_AConfiguredChord_PutsItFirst()
    {
        var chain = HotkeyChord.ChainFor("Ctrl+Shift+J");

        chain[0].Should().Be(new HotkeyChord(HotkeyModifiers.Control | HotkeyModifiers.Shift, "J"));
    }

    [Fact]
    public void ChainFor_AConfiguredChordThatIsAlreadyAFallback_DoesNotRepeatIt()
    {
        var chain = HotkeyChord.ChainFor("Ctrl+Alt+K");

        chain.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ChainFor_AnUnparseableChord_KeepsTheBuiltInChain()
    {
        var chain = HotkeyChord.ChainFor("not a chord at all");

        chain.Should().Equal(HotkeyChord.FallbackChain);
    }
}
