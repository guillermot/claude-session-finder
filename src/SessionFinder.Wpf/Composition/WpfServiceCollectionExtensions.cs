using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;
using SessionFinder.Wpf.Platform;
using SessionFinder.Wpf.Views;

namespace SessionFinder.Wpf.Composition;

/// <summary>
/// Registration entry point for the Windows adapters behind the presentation ports.
/// </summary>
/// <remarks>
/// Everything here is a singleton, and every one of them has thread affinity: the window, the
/// message-only window behind the hotkey and the notification-area icon all belong to the thread
/// that runs the message loop. Nothing resolves them anywhere else, which is why the affinity is
/// stated here rather than defended by a check at every call site.
/// </remarks>
internal static class WpfServiceCollectionExtensions
{
    /// <summary>
    /// Registers the window, the tray icon, the system-wide hotkey, the dispatcher adapter and the
    /// adapters the result actions run through.
    /// </summary>
    /// <param name="services">The service collection being built by the head.</param>
    /// <param name="uiDispatcher">The dispatcher of the thread that will run the message loop.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddWindowsShell(
        this IServiceCollection services,
        Dispatcher uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(uiDispatcher);

        services.TryAddSingleton<IUiDispatcher>(new WpfUiDispatcher(uiDispatcher));

        services.TryAddSingleton<IEditorLocator, VsCodeLocator>();
        services.TryAddSingleton<ITerminalLocator, TerminalLocator>();
        services.TryAddSingleton<IFileManagerLocator, ExplorerFileManagerLocator>();
        services.TryAddSingleton<ITerminalCommandFactory, WindowsTerminalCommandFactory>();
        services.TryAddSingleton<IShellLauncher, ProcessShellLauncher>();
        services.TryAddSingleton<IClipboardService>(new WpfClipboardService(uiDispatcher));

        services.TryAddSingleton<IAutostart, RegistryAutostart>();

        services.TryAddSingleton<NotifyIconTray>();
        services.TryAddSingleton<ITrayIcon>(provider => provider.GetRequiredService<NotifyIconTray>());
        services.TryAddSingleton<IUserNotifier>(CreateNotifier);

        services.TryAddSingleton<Win32GlobalHotkey>();
        services.TryAddSingleton<IGlobalHotkey>(provider => provider.GetRequiredService<Win32GlobalHotkey>());

        services.TryAddSingleton<SearchWindow>();
        services.TryAddSingleton<IAppWindow>(provider => provider.GetRequiredService<SearchWindow>());

        services.TryAddTransient<SettingsWindow>();
        services.TryAddTransient<RecapWindow>();

        return services;
    }

    /// <summary>
    /// Wraps the notification-area icon so that everything the user is told is written to the log
    /// as well. A balloon can be suppressed by the operating system without saying so; the log
    /// cannot, and it is the only record of a failure the user dismissed or never saw.
    /// </summary>
    private static IUserNotifier CreateNotifier(IServiceProvider provider) => new LoggingUserNotifier(
        provider.GetRequiredService<NotifyIconTray>(),
        provider.GetRequiredService<ILogger<LoggingUserNotifier>>());
}
