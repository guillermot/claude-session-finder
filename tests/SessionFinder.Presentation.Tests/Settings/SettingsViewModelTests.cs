using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Settings;
using SessionFinder.Presentation.Shell;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Settings;

public sealed class SettingsViewModelTests
{
    private const string ConfiguredChord = "Ctrl+Shift+J";
    private const string TypedChord = "ctrl+shift+u";
    private const string NormalizedTypedChord = "Ctrl+Shift+U";

    [Fact]
    public async Task LoadAsync_SettingsAreStored_FillsTheFormFromThem()
    {
        using var fixture = new Fixture();
        fixture.Handlers.Holds(fixture.SettingsWith(hotkey: ConfiguredChord));

        await fixture.ViewModel.LoadAsync(CancellationToken.None);

        fixture.ViewModel.Hotkey.Should().Be(ConfiguredChord);
    }

    [Fact]
    public async Task LoadAsync_TheIndexPathIsShown_SaysThatChangingItNeedsARestart()
    {
        using var fixture = new Fixture();

        await fixture.ViewModel.LoadAsync(CancellationToken.None);

        fixture.ViewModel.IndexPathNotice.Should().Contain("restart");
    }

    [Fact]
    public async Task LoadAsync_AChordIsRegistered_ShowsWhichOneIsActuallyInForce()
    {
        using var fixture = new Fixture();
        fixture.Registrar.Start();

        await fixture.ViewModel.LoadAsync(CancellationToken.None);

        fixture.ViewModel.RegisteredChord.Should().Be(HotkeyChord.Default.ToString());
    }

    [Fact]
    public async Task SaveAsync_TheChordIsWellFormed_StoresTheWholeForm()
    {
        using var fixture = new Fixture();
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.Hotkey = TypedChord;
        fixture.ViewModel.MaxResults = 12;

        await fixture.ViewModel.SaveAsync(CancellationToken.None);

        fixture.Handlers.Saves.Should().ContainSingle()
            .Which.MaxResults.Should().Be(12);
    }

    [Fact]
    public async Task SaveAsync_TheChordIsWrittenInAnyCasing_StoresItInOneSpelling()
    {
        using var fixture = new Fixture();
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.Hotkey = TypedChord;

        await fixture.ViewModel.SaveAsync(CancellationToken.None);

        fixture.Handlers.Saves[0].Hotkey.Should().Be(NormalizedTypedChord);
    }

    [Fact]
    public async Task SaveAsync_TheChordIsNotAChord_ExplainsItInsteadOfStoringIt()
    {
        using var fixture = new Fixture();
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.Hotkey = "Space";

        await fixture.ViewModel.SaveAsync(CancellationToken.None);

        fixture.Handlers.Saves.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_TheChordIsNotAChord_SaysWhatAChordLooksLike()
    {
        using var fixture = new Fixture();
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.ViewModel.Hotkey = "Space";

        await fixture.ViewModel.SaveAsync(CancellationToken.None);

        fixture.ViewModel.ValidationMessage.Should().Contain("Ctrl+Alt+Space");
    }

    [Fact]
    public async Task SaveAsync_TheSettingsAreStored_AnnouncesThatTheWindowCanClose()
    {
        using var fixture = new Fixture();
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        var wasAnnounced = false;
        fixture.ViewModel.Saved += (_, _) => wasAnnounced = true;

        await fixture.ViewModel.SaveAsync(CancellationToken.None);

        wasAnnounced.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_TheFileCannotBeWritten_TellsTheUserAndKeepsTheWindowOpen()
    {
        using var fixture = new Fixture();
        await fixture.ViewModel.LoadAsync(CancellationToken.None);
        fixture.Handlers.Refuses(AppError.SettingsNotSaved("the disk is full"));
        var wasAnnounced = false;
        fixture.ViewModel.Saved += (_, _) => wasAnnounced = true;

        await fixture.ViewModel.SaveAsync(CancellationToken.None);

        wasAnnounced.Should().BeFalse();
        fixture.Notifier.Notifications.Should().ContainSingle()
            .Which.Message.Should().Contain("the disk is full");
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Handlers = new FakeSettingsHandlers();
            Notifier = new RecordingUserNotifier();
            Registrar = new HotkeyRegistrar(
                FakeGlobalHotkey.GrantingEverything(),
                new MutableOptionsMonitor<ShellOptions>(new ShellOptions()),
                new InlineUiDispatcher(),
                new FakeTimeProvider(),
                NullLogger<HotkeyRegistrar>.Instance);

            ViewModel = new SettingsViewModel(Handlers, Handlers, Registrar, TestGuard.Over(Notifier));
        }

        public FakeSettingsHandlers Handlers { get; }

        public RecordingUserNotifier Notifier { get; }

        public HotkeyRegistrar Registrar { get; }

        public SettingsViewModel ViewModel { get; }

        public Core.Features.Settings.FinderSettings SettingsWith(string hotkey) =>
            FakeSettingsHandlers.Defaults() with { Hotkey = hotkey };

        public void Dispose()
        {
            ViewModel.Dispose();
            Registrar.Dispose();
        }
    }
}
