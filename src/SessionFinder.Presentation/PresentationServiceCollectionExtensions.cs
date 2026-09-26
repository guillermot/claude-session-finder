using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Presentation.Actions;
using SessionFinder.Presentation.Search;
using SessionFinder.Presentation.Settings;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation;

/// <summary>
/// Single registration entry point for the view models and the shell coordinator.
/// Only head projects are allowed to call it.
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the view models and presentation-level services.
    /// </summary>
    /// <remarks>
    /// Everything here is a singleton because there is exactly one search box, and a window that is
    /// hidden rather than closed keeps the same view model for the life of the process. That is also
    /// what makes reopening it instant.
    /// </remarks>
    /// <param name="services">The service collection being built by a head project.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSessionFinderPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(CreateDebouncer);
        services.TryAddSingleton<AppCommandGuard>();
        services.TryAddSingleton<SessionActionsViewModel>();
        services.TryAddSingleton<SearchViewModel>();
        services.TryAddSingleton<SettingsViewModel>();
        services.TryAddSingleton<HotkeyRegistrar>();
        services.TryAddSingleton<AutostartReconciler>();
        services.TryAddSingleton<IndexMaintenance>();
        services.TryAddSingleton<ShellCoordinator>();

        return services;
    }

    private static SearchDebouncer CreateDebouncer(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<ShellOptions>>().Value;

        return new SearchDebouncer(
            provider.GetRequiredService<TimeProvider>(),
            TimeSpan.FromMilliseconds(options.DebounceMilliseconds));
    }
}
