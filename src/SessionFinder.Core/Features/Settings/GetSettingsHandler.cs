using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;

namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Reads the settings from the same bound options every other part of the application reads, so
/// the window can never show something different from what is in effect.
/// </summary>
/// <remarks>
/// The monitors are read rather than the snapshots, because the settings file is reloaded while the
/// application runs and a window opened after an external edit must show the edit.
/// </remarks>
public sealed class GetSettingsHandler(
    IOptionsMonitor<ShellOptions> shell,
    IOptionsMonitor<SearchOptions> search,
    IOptionsMonitor<LogLevelOptions> logLevel,
    IApplicationPaths paths) : IGetSettingsHandler
{
    /// <inheritdoc />
    public Task<GetSettingsResult> HandleAsync(GetSettingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        cancellationToken.ThrowIfCancellationRequested();

        var result = new GetSettingsResult(
            FinderSettings.From(shell.CurrentValue, search.CurrentValue, logLevel.CurrentValue),
            paths.SettingsFilePath,
            paths.IndexFilePath,
            paths.LogFolderPath);

        return Task.FromResult(result);
    }
}
