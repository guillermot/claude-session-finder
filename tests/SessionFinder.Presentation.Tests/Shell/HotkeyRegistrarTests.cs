using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SessionFinder.Core.Configuration;
using SessionFinder.Presentation.Shell;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Shell;

public sealed class HotkeyRegistrarTests
{
    private static readonly TimeSpan PastTheDebounce = TimeSpan.FromMilliseconds(300);
    private const string FreeChord = "Ctrl+Shift+J";
    private const string AnotherFreeChord = "Ctrl+Shift+U";

    [Fact]
    public void Start_TheDefaultChordIsFree_RegistersIt()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything());

        fixture.Registrar.Start();

        fixture.Registrar.RegisteredChord.Should().Be(HotkeyChord.Default);
    }

    [Fact]
    public void Start_TheDefaultChordIsTaken_FallsForwardToTheNextCandidate()
    {
        using var fixture = new Fixture(new FakeGlobalHotkey(chord => chord != HotkeyChord.Default));

        fixture.Registrar.Start();

        fixture.Registrar.RegisteredChord.Should().Be(HotkeyChord.FallbackChain[1]);
    }

    [Fact]
    public void Start_EveryChordIsTaken_LeavesNoChordRegistered()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingNothing());

        fixture.Registrar.Start();

        fixture.Registrar.RegisteredChord.Should().BeNull();
    }

    [Fact]
    public void Start_EveryChordIsTaken_ReportsWhichChordsWereTried()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingNothing());

        fixture.Registrar.Start();

        fixture.Registrar.LastAttempt.Should().BeEquivalentTo(HotkeyChord.FallbackChain);
    }

    [Fact]
    public void Start_AConfiguredChord_IsTriedBeforeTheBuiltInOnes()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything(), FreeChord);

        fixture.Registrar.Start();

        fixture.Registrar.RegisteredChord!.ToString().Should().Be(FreeChord);
    }

    [Fact]
    public void Start_CalledTwice_RegistersTheChordOnce()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        using var fixture = new Fixture(hotkey);

        fixture.Registrar.Start();
        fixture.Registrar.Start();

        hotkey.Attempts.Should().ContainSingle();
    }

    [Fact]
    public void SettingChanged_ADifferentChord_RegistersItWithoutARestart()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything(), FreeChord);
        fixture.Registrar.Start();

        fixture.ChangeHotkeyTo(AnotherFreeChord);

        fixture.Registrar.RegisteredChord!.ToString().Should().Be(AnotherFreeChord);
    }

    [Fact]
    public void SettingChanged_ADifferentChord_IsNotAppliedBeforeTheSettlingDelay()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything(), FreeChord);
        fixture.Registrar.Start();

        fixture.Options.Set(new ShellOptions { Hotkey = AnotherFreeChord });

        fixture.Registrar.RegisteredChord!.ToString().Should().Be(FreeChord);
    }

    [Fact]
    public void SettingChanged_TheSameChordAsBefore_DoesNotTouchTheRegistration()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        using var fixture = new Fixture(hotkey, FreeChord);
        fixture.Registrar.Start();

        fixture.ChangeHotkeyTo(FreeChord);

        hotkey.Attempts.Should().ContainSingle();
    }

    [Fact]
    public void SettingChanged_TheWatcherReportsOneSaveTwice_RegistersOnlyOnceMore()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        using var fixture = new Fixture(hotkey, FreeChord);
        fixture.Registrar.Start();

        fixture.Options.SetAndNotifyTwice(new ShellOptions { Hotkey = AnotherFreeChord });
        fixture.Time.Advance(PastTheDebounce);

        hotkey.Attempts.Should().HaveCount(2);
    }

    [Fact]
    public void SettingChanged_AChordWasRegistered_AnnouncesTheNewRegistration()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything(), FreeChord);
        fixture.Registrar.Start();
        var announcements = 0;
        fixture.Registrar.RegistrationChanged += (_, _) => announcements++;

        fixture.ChangeHotkeyTo(AnotherFreeChord);

        announcements.Should().Be(1);
    }

    [Fact]
    public void Dispose_AfterStarting_ReleasesTheChord()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        var fixture = new Fixture(hotkey);
        fixture.Registrar.Start();

        fixture.Dispose();

        hotkey.UnregisterCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_ASettingChangeWasPending_DoesNotRegisterAfterwards()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        var fixture = new Fixture(hotkey, FreeChord);
        fixture.Registrar.Start();
        fixture.Options.Set(new ShellOptions { Hotkey = AnotherFreeChord });

        fixture.Dispose();
        fixture.Time.Advance(PastTheDebounce);

        hotkey.Attempts.Should().ContainSingle();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(FakeGlobalHotkey hotkey, string? configuredHotkey = null)
        {
            Time = new FakeTimeProvider();
            Options = new MutableOptionsMonitor<ShellOptions>(new ShellOptions { Hotkey = configuredHotkey });
            Registrar = new HotkeyRegistrar(
                hotkey,
                Options,
                new InlineUiDispatcher(),
                Time,
                NullLogger<HotkeyRegistrar>.Instance);
        }

        public FakeTimeProvider Time { get; }

        public MutableOptionsMonitor<ShellOptions> Options { get; }

        public HotkeyRegistrar Registrar { get; }

        public void ChangeHotkeyTo(string chord)
        {
            Options.Set(new ShellOptions { Hotkey = chord });
            Time.Advance(PastTheDebounce);
        }

        public void Dispose() => Registrar.Dispose();
    }
}
