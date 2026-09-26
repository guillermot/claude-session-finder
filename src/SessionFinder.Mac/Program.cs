using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionFinder.Mac.Composition;
using SessionFinder.Mac.Diagnostics;
using SessionFinder.Mac.Platform;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Mac;

/// <summary>
/// Entry point for the macOS head.
/// </summary>
/// <remarks>
/// <para>
/// The order below is the whole design of the start-up: the menu-bar item and the chord come up
/// immediately, on this thread, because they are what the user is waiting for, and the index comes
/// up alongside them on a thread-pool thread, because the first pass over the transcripts takes
/// long enough to be felt if the window is waiting behind it.
/// </para>
/// <para>
/// Everything after <c>SetupWithLifetime</c> runs with Avalonia composed but its run loop not yet
/// started, which is the only window in which the shell can be built on the thread that will own
/// it. The loop is entered explicitly at the end rather than by <c>StartWithClassicDesktopLifetime</c>,
/// because that helper gives no such window.
/// </para>
/// <para>
/// Shutting down only when asked is what makes this a menu-bar application rather than a window:
/// the search box is hidden far more often than it is shown, and under any other shutdown mode the
/// first dismissal would end the process.
/// </para>
/// </remarks>
internal static class Program
{
    private const int ExitSuccess = 0;

    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(5);

    [STAThread]
    private static int Main(string[] args)
    {
        using var gate = SingleInstanceGate.Acquire();

        if (!gate.IsPrimary)
        {
            gate.SignalPrimary();

            return ExitSuccess;
        }

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args,
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        BuildAvaloniaApp().SetupWithLifetime(lifetime);

        using var host = HostFactory.Create(ShutdownBudget);

        InstallExceptionHandlers(host.Services);

        using var shell = MacShell.Start(host.Services, gate);

        var cycle = CreateLifecycle(host, lifetime);

        shell.ExitRequested += cycle.OnExitRequested;

        cycle.Begin();

        return lifetime.Start(args);
    }

    /// <remarks>
    /// Keeping out of the Dock is also declared in Info.plist, but LaunchServices only reads that
    /// when it is the one starting the application. An IDE or <c>dotnet run</c> executes the binary
    /// directly, so the platform is told as well, and the application behaves the same however it
    /// was started.
    /// </remarks>
    private static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .With(new MacOSPlatformOptions { ShowInDock = false })
        .WithInterFont()
        .LogToTrace();

    /// <summary>
    /// Subscribes the process-wide failure handlers before anything can fail.
    /// </summary>
    /// <remarks>
    /// This goes after the host is composed, because the handlers need somewhere to log and
    /// something to notify with, and before the shell starts, because the shell is the first thing
    /// that can throw. Between the two is the whole window.
    /// </remarks>
    private static void InstallExceptionHandlers(IServiceProvider services) =>
        GlobalExceptionHandlers.Install(
            services.GetRequiredService<IUserNotifier>(),
            services.GetRequiredService<ILogger<GlobalExceptionHandlers>>());

    /// <summary>
    /// Assembles the lifecycle here rather than in the container, because it is the one object that
    /// spans both sides of the composition root: it holds the host it was built from.
    /// </summary>
    private static HostLifecycle CreateLifecycle(
        IHost host,
        IClassicDesktopStyleApplicationLifetime lifetime) => new(
        host,
        lifetime,
        host.Services.GetRequiredService<IUiDispatcher>(),
        host.Services.GetRequiredService<IUserNotifier>(),
        host.Services.GetRequiredService<ILogger<HostLifecycle>>(),
        ShutdownBudget);
}
