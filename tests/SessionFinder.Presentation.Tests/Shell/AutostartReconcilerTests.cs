using Microsoft.Extensions.Logging.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Shell;
using SessionFinder.Presentation.Tests.Fakes;

namespace SessionFinder.Presentation.Tests.Shell;

public sealed class AutostartReconcilerTests
{
    [Fact]
    public void Start_TheRegistrationDisagreesWithTheSetting_RewritesIt()
    {
        using var fixture = new Fixture(registered: false, setting: true);

        fixture.Reconciler.Start();

        fixture.Autostart.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Start_TheSettingIsOffAndTheRegistrationIsOn_RemovesIt()
    {
        using var fixture = new Fixture(registered: true, setting: false);

        fixture.Reconciler.Start();

        fixture.Autostart.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Start_TheRegistrationAlreadyAgrees_WritesNothing()
    {
        using var fixture = new Fixture(registered: true, setting: true);

        fixture.Reconciler.Start();

        fixture.Autostart.Writes.Should().BeEmpty();
    }

    [Fact]
    public void SettingChanged_TheUserTurnedItOn_RegistersWithoutARestart()
    {
        using var fixture = new Fixture(registered: false, setting: false);
        fixture.Reconciler.Start();

        fixture.Options.Set(new ShellOptions { StartAtLogin = true });

        fixture.Autostart.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SettingChanged_TheWatcherReportsOneSaveTwice_WritesOnce()
    {
        using var fixture = new Fixture(registered: false, setting: false);
        fixture.Reconciler.Start();

        fixture.Options.SetAndNotifyTwice(new ShellOptions { StartAtLogin = true });

        fixture.Autostart.Writes.Should().ContainSingle();
    }

    [Fact]
    public void Start_TheRegistrationCannotBeRead_TellsTheUserAndCarriesOn()
    {
        using var fixture = new Fixture(registered: false, setting: true);
        fixture.Autostart.Refuses(AppError.AutostartUnavailable("policy says no"));

        fixture.Reconciler.Start();

        fixture.Notifier.Notifications.Should().ContainSingle()
            .Which.Message.Should().Contain("policy says no");
    }

    [Fact]
    public void Dispose_TheSettingChangesAfterwards_LeavesTheRegistrationAlone()
    {
        var fixture = new Fixture(registered: false, setting: false);
        fixture.Reconciler.Start();
        fixture.Dispose();

        fixture.Options.Set(new ShellOptions { StartAtLogin = true });

        fixture.Autostart.Writes.Should().BeEmpty();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(bool registered, bool setting)
        {
            Autostart = new FakeAutostart(registered);
            Options = new MutableOptionsMonitor<ShellOptions>(new ShellOptions { StartAtLogin = setting });
            Notifier = new RecordingUserNotifier();
            Reconciler = new AutostartReconciler(
                Autostart,
                Options,
                Notifier,
                NullLogger<AutostartReconciler>.Instance);
        }

        public FakeAutostart Autostart { get; }

        public MutableOptionsMonitor<ShellOptions> Options { get; }

        public RecordingUserNotifier Notifier { get; }

        public AutostartReconciler Reconciler { get; }

        public void Dispose() => Reconciler.Dispose();
    }
}
