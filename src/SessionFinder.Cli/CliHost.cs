using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionFinder.Core;
using SessionFinder.Infrastructure;

namespace SessionFinder.Cli;

/// <summary>
/// Composition root of the diagnostic head. Commands that need the engine build one of these and
/// let it dispose the SQLite connection on the way out.
/// </summary>
internal static class CliHost
{
    private const string EventTimestampFormat = "HH:mm:ss.fff ";

    /// <summary>
    /// Builds a host with the Core handlers and the Infrastructure adapters registered, for a
    /// command that does its work and exits.
    /// </summary>
    /// <returns>A host the caller must dispose.</returns>
    public static IHost Build()
    {
        var builder = CreateBuilder(LogLevel.Warning, timestamped: false);

        return builder.Build();
    }

    /// <summary>
    /// Builds a host that additionally runs the background indexer.
    /// </summary>
    /// <remarks>
    /// Informational logging is on rather than off, because for a command whose whole output is the
    /// account of what the indexer did, silencing it would leave nothing to watch. The shutdown
    /// budget is passed in so the wait for the writer to finish the transcript in hand is bounded by
    /// the same number the command reports against.
    /// </remarks>
    /// <param name="shutdownTimeout">How long the host waits for the indexer to stop.</param>
    /// <returns>A host the caller must dispose.</returns>
    public static IHost BuildIndexer(TimeSpan shutdownTimeout)
    {
        var builder = CreateBuilder(LogLevel.Information, timestamped: true);

        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = shutdownTimeout);
        builder.Services.AddSessionFinderIndexer();

        return builder.Build();
    }

    private static HostApplicationBuilder CreateBuilder(LogLevel minimumLevel, bool timestamped)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = timestamped ? EventTimestampFormat : null;
        });
        builder.Logging.SetMinimumLevel(minimumLevel);

        builder.Services.AddSessionFinderCore(builder.Configuration);
        builder.Services.AddSessionFinderInfrastructure(builder.Configuration);

        return builder;
    }
}
