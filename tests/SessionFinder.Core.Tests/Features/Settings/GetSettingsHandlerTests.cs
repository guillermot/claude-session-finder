using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.Settings;
using SessionFinder.Core.Tests.Features.Search;

namespace SessionFinder.Core.Tests.Features.Settings;

public sealed class GetSettingsHandlerTests
{
    private const string ConfiguredChord = "Ctrl+Shift+J";

    [Fact]
    public async Task HandleAsync_AChordIsConfigured_ReportsIt()
    {
        var handler = CreateHandler(new ShellOptions { Hotkey = ConfiguredChord });

        var result = await handler.HandleAsync(GetSettingsQuery.Instance, CancellationToken.None);

        result.Settings.Hotkey.Should().Be(ConfiguredChord);
    }

    [Fact]
    public async Task HandleAsync_TheWindowNeedsToShowThem_ReportsTheThreeLocations()
    {
        var handler = CreateHandler(new ShellOptions());

        var result = await handler.HandleAsync(GetSettingsQuery.Instance, CancellationToken.None);

        result.SettingsFilePath.Should().Be(StubPaths.Settings);
        result.IndexFilePath.Should().Be(StubPaths.Index);
        result.LogFolderPath.Should().Be(StubPaths.Logs);
    }

    private static GetSettingsHandler CreateHandler(ShellOptions shell) => new(
        new FixedOptionsMonitor<ShellOptions>(shell),
        new FixedOptionsMonitor<SearchOptions>(new SearchOptions()),
        new FixedOptionsMonitor<LogLevelOptions>(new LogLevelOptions()),
        new StubPaths());

    private sealed class StubPaths : IApplicationPaths
    {
        public const string Settings = @"C:\settings\settings.json";
        public const string Index = @"C:\index\index.db";
        public const string Logs = @"C:\logs";

        public string SettingsFilePath => Settings;

        public string IndexFilePath => Index;

        public string LogFolderPath => Logs;
    }
}
