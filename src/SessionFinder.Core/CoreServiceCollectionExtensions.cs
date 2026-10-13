using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.DailyRecap;
using SessionFinder.Core.Features.IndexSessionFile;
using SessionFinder.Core.Features.IndexStatus;
using SessionFinder.Core.Features.ReconcileIndex;
using SessionFinder.Core.Features.Search;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Features.Settings;

namespace SessionFinder.Core;

/// <summary>
/// Single registration entry point for the domain rules and use-case handlers that live in
/// <c>SessionFinder.Core</c>. Only head projects are allowed to call it.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Core use-case handlers and binds <see cref="FinderOptions"/> from configuration.
    /// </summary>
    /// <param name="services">The service collection being built by a head project.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSessionFinderCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FinderOptions>()
            .Bind(configuration.GetSection(FinderOptions.SectionName));

        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetSection(SearchOptions.SectionName));

        services.AddOptions<ShellOptions>()
            .Bind(configuration.GetSection(ShellOptions.SectionName));

        services.AddOptions<LogLevelOptions>()
            .Bind(configuration.GetSection(LogLevelOptions.SectionName));

        services.AddOptions<RecapOptions>()
            .Bind(configuration.GetSection(RecapOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIndexSessionFileHandler, IndexSessionFileHandler>();
        services.TryAddSingleton<IReconcileIndexHandler, ReconcileIndexHandler>();
        services.TryAddSingleton<IGetIndexStatusHandler, GetIndexStatusHandler>();
        services.TryAddSingleton<ISearchSessionsHandler, SearchSessionsHandler>();
        services.TryAddSingleton<IOpenFolderInEditorHandler, OpenFolderInEditorHandler>();
        services.TryAddSingleton<IRevealInFileExplorerHandler, RevealInFileExplorerHandler>();
        services.TryAddSingleton<IResumeSessionHandler, ResumeSessionHandler>();
        services.TryAddSingleton<ICopySessionDetailHandler, CopySessionDetailHandler>();
        services.TryAddSingleton<IGetSettingsHandler, GetSettingsHandler>();
        services.TryAddSingleton<IUpdateSettingsHandler, UpdateSettingsHandler>();
        services.TryAddSingleton<IGetDailyRecapHandler, GetDailyRecapHandler>();
        services.TryAddSingleton<ISummarizeRecapHandler, SummarizeRecapHandler>();

        return services;
    }
}
