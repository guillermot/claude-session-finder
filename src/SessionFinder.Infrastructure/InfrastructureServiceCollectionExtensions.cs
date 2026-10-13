using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.Settings;
using SessionFinder.Infrastructure.FileSystem;
using SessionFinder.Infrastructure.Indexing;
using SessionFinder.Infrastructure.Persistence;
using SessionFinder.Infrastructure.Processes;
using SessionFinder.Infrastructure.Recap;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Infrastructure;

/// <summary>
/// Single registration entry point for the transcript parser, the SQLite index and the
/// file-system adapters. Only head projects are allowed to call it.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the adapters that implement the Core ports.
    /// </summary>
    /// <param name="services">The service collection being built by a head project.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSessionFinderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<IApplicationPaths, ApplicationPaths>();
        services.TryAddSingleton<ISettingsStore, JsonSettingsStore>();

        services.TryAddSingleton<ISessionTranscriptParser, JsonlSessionTranscriptParser>();
        services.TryAddSingleton<ITranscriptFileReader, FileSystemTranscriptReader>();
        services.TryAddSingleton<ISessionFileCatalog, ClaudeProjectsCatalog>();

        services.TryAddSingleton<SqliteIndexDatabase>();
        services.TryAddSingleton<ISessionIndexWriter, SqliteSessionIndexWriter>();
        services.TryAddSingleton<SqliteSessionIndexReader>();
        services.TryAddSingleton<ISessionIndexReader>(provider => provider.GetRequiredService<SqliteSessionIndexReader>());
        services.TryAddSingleton<ISessionActivityReader>(provider => provider.GetRequiredService<SqliteSessionIndexReader>());

        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<IGitActivityReader, GitCliActivityReader>();
        services.TryAddSingleton<IRecapSummarizer, ClaudeCliRecapSummarizer>();

        return services;
    }

    /// <summary>
    /// Registers the background indexer: the watcher, the buffer that folds its reports by path, and
    /// the hosted service that turns them into writes.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="AddSessionFinderInfrastructure"/> because it is a decision, not a
    /// detail: a head that runs one command and exits wants the adapters without a background
    /// service holding the only writer connection open behind it.
    /// </remarks>
    /// <param name="services">The service collection being built by a head project.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSessionFinderIndexer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<SessionFileWatcher>();
        services.TryAddSingleton<PendingChangeBuffer>();
        services.AddHostedService<IndexerHostedService>();

        return services;
    }
}
