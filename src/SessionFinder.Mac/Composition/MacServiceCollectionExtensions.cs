using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Mac.Platform;
using SessionFinder.Mac.Views;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Mac.Composition;

/// <summary>
/// Registration entry point for the macOS adapters behind the presentation ports.
/// </summary>
/// <remarks>
/// Everything here is a singleton, and most of them have thread affinity: the window, the menu-bar
/// item and the Carbon hotkey all belong to the thread that runs the run loop. Nothing resolves
/// them anywhere else, which is why the affinity is stated here rather than defended by a check at
/// every call site.
/// </remarks>
internal static class MacServiceCollectionExtensions
{
    /// <summary>
    /// Registers the window, the menu-bar item, the system-wide chord, the dispatcher adapter and
    /// the adapters the result actions run through.
    /// </summary>
    /// <param name="services">The service collection being built by the head.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMacShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();

        services.TryAddSingleton<IEditorLocator, MacEditorLocator>();
        services.TryAddSingleton<ITerminalLocator, MacTerminalLocator>();
        services.TryAddSingleton<IFileManagerLocator, FinderFileManagerLocator>();
        services.TryAddSingleton<ITerminalCommandFactory, MacTerminalCommandFactory>();
        services.TryAddSingleton<IShellLauncher, MacShellLauncher>();
        services.TryAddSingleton<IClipboardService, AvaloniaClipboardService>();

        services.TryAddSingleton<IAutostart, LaunchAgentAutostart>();

        services.TryAddSingleton<StatusItemTray>();
        services.TryAddSingleton<ITrayIcon>(provider => provider.GetRequiredService<StatusItemTray>());
        services.TryAddSingleton<IUserNotifier>(CreateNotifier);

        services.TryAddSingleton<CarbonGlobalHotkey>();
        services.TryAddSingleton<IGlobalHotkey>(provider => provider.GetRequiredService<CarbonGlobalHotkey>());

        services.TryAddSingleton<SearchWindow>();
        services.TryAddSingleton<IAppWindow>(provider => provider.GetRequiredService<SearchWindow>());

        services.TryAddTransient<SettingsWindow>();

        return services;
    }

    /// <summary>
    /// Wraps the notifier so that everything the user is told is written to the log as well. macOS
    /// can decline to show a notification from an application it does not recognise without saying
    /// so, and the log is then the only record of a failure the user never saw.
    /// </summary>
    private static IUserNotifier CreateNotifier(IServiceProvider provider) => new LoggingUserNotifier(
        new OsaScriptNotifier(provider.GetRequiredService<ILogger<OsaScriptNotifier>>()),
        provider.GetRequiredService<ILogger<LoggingUserNotifier>>());
}
