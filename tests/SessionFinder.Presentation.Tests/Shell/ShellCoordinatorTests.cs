using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SessionFinder.Core.Configuration;
using SessionFinder.Presentation.Shell;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Shell;

public sealed class ShellCoordinatorTests
{
    [Fact]
    public void Start_AChordIsRegistered_NamesItInTheTrayTooltip()
    {
        using var fixture = new Fixture(new FakeGlobalHotkey(chord => chord != HotkeyChord.Default));

        fixture.Coordinator.Start();

        fixture.Tray.Tooltip.Should().Contain(HotkeyChord.FallbackChain[1].ToString());
    }

    [Fact]
    public void Start_EveryChordIsTaken_TellsTheUserWhichChordsFailed()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingNothing());

        fixture.Coordinator.Start();

        fixture.Notifier.Notifications.Should().ContainSingle()
            .Which.Message.Should().Contain(HotkeyChord.Default.ToString());
    }

    [Fact]
    public void Start_EveryChordIsTaken_KeepsTheTrayMenuAsTheWayIn()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingNothing());
        fixture.Coordinator.Start();

        fixture.Tray.RaiseSearchRequested();

        fixture.Window.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void HotkeyPressed_TheWindowIsHidden_ShowsIt()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        using var fixture = new Fixture(hotkey);
        fixture.Coordinator.Start();

        hotkey.RaisePressed();

        fixture.Window.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void HotkeyPressed_TheWindowIsShowing_HidesIt()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        using var fixture = new Fixture(hotkey);
        fixture.Coordinator.Start();
        hotkey.RaisePressed();

        hotkey.RaisePressed();

        fixture.Window.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void TrayExit_TheMenuEntryIsPicked_AsksTheHeadToShutDown()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything());
        fixture.Coordinator.Start();
        var wasRequested = false;
        fixture.Coordinator.ExitRequested += (_, _) => wasRequested = true;

        fixture.Tray.RaiseExitRequested();

        wasRequested.Should().BeTrue();
    }

    [Fact]
    public void TraySettings_TheMenuEntryIsPicked_AsksTheHeadForTheSettingsWindow()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything());
        fixture.Coordinator.Start();
        var wasRequested = false;
        fixture.Coordinator.SettingsRequested += (_, _) => wasRequested = true;

        fixture.Tray.RaiseSettingsRequested();

        wasRequested.Should().BeTrue();
    }

    [Fact]
    public void TrayRebuildIndex_TheMenuEntryIsPicked_ReadsEveryTranscriptAgain()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything());
        fixture.Coordinator.Start();

        fixture.Tray.RaiseRebuildIndexRequested();

        fixture.Reconcile.Commands.Should().ContainSingle()
            .Which.ForceFullReparse.Should().BeTrue();
    }

    [Fact]
    public void TrayRebuildIndex_ThePassThrows_ReportsItInsteadOfEndingTheApplication()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything());
        fixture.Reconcile.Throws(new InvalidOperationException("the index is gone"));
        fixture.Coordinator.Start();

        fixture.Tray.RaiseRebuildIndexRequested();

        fixture.Notifier.Notifications.Should().ContainSingle(notification =>
            notification.Title == "Could not rebuild the index");
    }

    [Fact]
    public void TrayLogFolder_TheMenuEntryIsPicked_ShowsTheFolderInTheFileManager()
    {
        using var fixture = new Fixture(FakeGlobalHotkey.GrantingEverything());
        fixture.Coordinator.Start();

        fixture.Tray.RaiseLogFolderRequested();

        fixture.Handlers.Calls.Should().ContainSingle()
            .Which.Should().Be(nameof(Core.Features.SessionActions.RevealInFileExplorerCommand));
    }

    [Fact]
    public void Dispose_AfterStarting_StopsRespondingToTheChord()
    {
        var hotkey = FakeGlobalHotkey.GrantingEverything();
        var fixture = new Fixture(hotkey);
        fixture.Coordinator.Start();
        fixture.Dispose();

        hotkey.RaisePressed();

        fixture.Window.IsVisible.Should().BeFalse();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(FakeGlobalHotkey hotkey)
        {
            Window = new FakeAppWindow();
            Tray = new FakeTrayIcon();
            Notifier = new RecordingUserNotifier();
            Reconcile = new FakeReconcileIndexHandler();
            Handlers = new FakeSessionActionHandlers();

            var registrar = new HotkeyRegistrar(
                hotkey,
                new MutableOptionsMonitor<ShellOptions>(new ShellOptions()),
                new InlineUiDispatcher(),
                new FakeTimeProvider(),
                NullLogger<HotkeyRegistrar>.Instance);

            Coordinator = new ShellCoordinator(
                registrar,
                Window,
                Tray,
                Notifier,
                new IndexMaintenance(Reconcile, Handlers, new FakeApplicationPaths(), Notifier),
                TestGuard.Over(Notifier));
        }

        public FakeAppWindow Window { get; }

        public FakeTrayIcon Tray { get; }

        public RecordingUserNotifier Notifier { get; }

        public FakeReconcileIndexHandler Reconcile { get; }

        public FakeSessionActionHandlers Handlers { get; }

        public ShellCoordinator Coordinator { get; }

        public void Dispose() => Coordinator.Dispose();
    }
}
