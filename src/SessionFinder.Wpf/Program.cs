using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Wpf.Composition;
using SessionFinder.Wpf.Diagnostics;
using SessionFinder.Wpf.Platform;

namespace SessionFinder.Wpf;

/// <summary>
/// Explicit entry point for the Windows head.
/// </summary>
/// <remarks>
/// <para>
/// It is declared here rather than generated from <c>App.xaml</c>, which is compiled as a Page for
/// exactly this reason: the entry point WPF generates cannot be extended with a host that has to be
/// composed before the message loop, and two entry points in one assembly is a build error rather
/// than a choice.
/// </para>
/// <para>
/// The entry point is synchronous, which is not a matter of taste. An asynchronous <c>Main</c> is
/// compiled into a synthesized entry point that does not carry <see cref="STAThreadAttribute"/>, so
/// the process starts in the multi-threaded apartment and the first user interface component built
/// — here, the message-only window behind the chord — throws before anything appears. The host is
/// started and stopped by <see cref="HostLifecycle"/> instead, which awaits both from inside the
/// loop rather than blocking this thread around it.
/// </para>
/// <para>
/// The order below is the whole design of the start-up: the tray icon and the chord come up
/// immediately, on this thread, because they are what the user is waiting for, and the index comes
/// up alongside them on a thread-pool thread, because the first pass over the transcripts takes long
/// enough to be felt if the window is waiting behind it.
/// </para>
/// </remarks>
internal static class Program
{
    private const int ExitSuccess = 0;

    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(5);

    [STAThread]
    private static int Main()
    {
        using var gate = SingleInstanceGate.Acquire();

        if (!gate.IsPrimary)
        {
            gate.SignalPrimary();
            return ExitSuccess;
        }

        var application = CreateApplication();

        using var host = HostFactory.Create(application.Dispatcher, ShutdownBudget);

        InstallExceptionHandlers(host.Services, application);

        using var shell = WindowsShell.Start(host.Services, gate);

        var lifecycle = CreateLifecycle(host, application);

        shell.ExitRequested += lifecycle.OnExitRequested;

        lifecycle.Begin();

        return application.Run();
    }

    /// <summary>
    /// Builds the application object and loads the resource dictionary by hand, which is the part
    /// the generated entry point would otherwise have done.
    /// </summary>
    /// <remarks>
    /// Shutting down only when asked is what makes this a tray application rather than a window:
    /// the search box is hidden far more often than it is shown, and under any other shutdown mode
    /// the first dismissal would end the process.
    /// </remarks>
    private static App CreateApplication()
    {
        var application = new App
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        application.InitializeComponent();

        return application;
    }

    /// <summary>
    /// Subscribes the process-wide failure handlers before anything can fail.
    /// </summary>
    /// <remarks>
    /// This goes after the host is composed, because the handlers need somewhere to log and
    /// something to notify with, and before the shell starts, because the shell is the first thing
    /// that can throw. Between the two is the whole window.
    /// </remarks>
    private static void InstallExceptionHandlers(IServiceProvider services, App application) =>
        GlobalExceptionHandlers.Install(
            application,
            services.GetRequiredService<IUserNotifier>(),
            services.GetRequiredService<ILogger<GlobalExceptionHandlers>>());

    /// <summary>
    /// Assembles the lifecycle here rather than in the container, because it is the one object that
    /// spans both sides of the composition root: it holds the host it was built from.
    /// </summary>
    private static HostLifecycle CreateLifecycle(IHost host, Application application) => new(
        host,
        application,
        host.Services.GetRequiredService<IUiDispatcher>(),
        host.Services.GetRequiredService<IUserNotifier>(),
        host.Services.GetRequiredService<ILogger<HostLifecycle>>(),
        ShutdownBudget);
}
