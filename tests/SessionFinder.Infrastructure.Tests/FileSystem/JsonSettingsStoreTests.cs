using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Settings;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Tests.FileSystem;

/// <summary>
/// The settings file is the one file this application writes as well as reads, so these tests use
/// the real configuration binder to read back what the store wrote. A round trip through the binder
/// is the only assertion that proves a saved setting will actually apply.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    private const string RootFolderName = "session-finder-settings";
    private const string SettingsFileName = "settings.json";
    private const string ConfiguredChord = "Ctrl+Shift+J";
    private const string HandEditedIndexPath = @"D:\caches\finder.db";

    private readonly string _directory;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), RootFolderName, Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);

        SettingsPath = Path.Combine(_directory, SettingsFileName);
        _store = new JsonSettingsStore(new StubPaths(SettingsPath));
    }

    private string SettingsPath { get; }

    [Fact]
    public async Task SaveAsync_NoFileExistedYet_CreatesOne()
    {
        await _store.SaveAsync(Defaults(), CancellationToken.None);

        File.Exists(SettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_AChordWasChosen_BindsBackOntoTheShellOptions()
    {
        await _store.SaveAsync(Defaults() with { Hotkey = ConfiguredChord }, CancellationToken.None);

        Bind<ShellOptions>(ShellOptions.SectionName).Hotkey.Should().Be(ConfiguredChord);
    }

    [Fact]
    public async Task SaveAsync_StartAtLoginWasTurnedOn_BindsBackOntoTheShellOptions()
    {
        await _store.SaveAsync(Defaults() with { StartAtLogin = true }, CancellationToken.None);

        Bind<ShellOptions>(ShellOptions.SectionName).StartAtLogin.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_RecapSettingsWereChanged_BindBackOntoTheRecapOptions()
    {
        var settings = Defaults() with { RecapDayStartHour = 6, RecapLookbackDays = 2, RecapIncludeGit = false, RecapAiSummary = true };

        await _store.SaveAsync(settings, CancellationToken.None);

        var recap = Bind<RecapOptions>(RecapOptions.SectionName);
        recap.DayStartHour.Should().Be(6);
        recap.LookbackDays.Should().Be(2);
        recap.IncludeGit.Should().BeFalse();
        recap.EnableAiSummary.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WeightsWereTuned_BindBackOntoTheSearchOptions()
    {
        var settings = Defaults() with { ChunkWeights = new ChunkWeights(9, 8, 7, 6, 5) };

        await _store.SaveAsync(settings, CancellationToken.None);

        Bind<SearchOptions>(SearchOptions.SectionName).ToChunkWeights().Title.Should().Be(9);
    }

    [Fact]
    public async Task SaveAsync_VerboseLoggingWasTurnedOn_WritesTheLevelTheFrameworkReads()
    {
        await _store.SaveAsync(Defaults() with { VerboseLogging = true }, CancellationToken.None);

        Bind<LogLevelOptions>(LogLevelOptions.SectionName).IsVerbose.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_VerboseLoggingWasTurnedOff_WritesTheOrdinaryLevel()
    {
        await _store.SaveAsync(Defaults() with { VerboseLogging = true }, CancellationToken.None);

        await _store.SaveAsync(Defaults() with { VerboseLogging = false }, CancellationToken.None);

        Bind<LogLevelOptions>(LogLevelOptions.SectionName).IsVerbose.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_TheFileHoldsASettingTheWindowDoesNotOffer_LeavesItAlone()
    {
        var escapedPath = HandEditedIndexPath.Replace("\\", "\\\\", StringComparison.Ordinal);

        await File.WriteAllTextAsync(
            SettingsPath,
            $$$"""{"Finder":{"IndexPath":"{{{escapedPath}}}"}}""");

        await _store.SaveAsync(Defaults(), CancellationToken.None);

        Bind<FinderOptions>(FinderOptions.SectionName).IndexPath.Should().Be(HandEditedIndexPath);
    }

    [Fact]
    public async Task SaveAsync_NoEditorWasChosen_LeavesNoEmptyValueBehind()
    {
        await _store.SaveAsync(Defaults() with { EditorPath = "   " }, CancellationToken.None);

        Bind<ShellOptions>(ShellOptions.SectionName).EditorPath.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_AnEditorWasChosen_BindsBackOntoTheShellOptions()
    {
        const string editor = @"C:\Program Files\Editor\editor.exe";

        await _store.SaveAsync(Defaults() with { EditorPath = editor }, CancellationToken.None);

        Bind<ShellOptions>(ShellOptions.SectionName).EditorPath.Should().Be(editor);
    }

    [Fact]
    public async Task SaveAsync_TheExistingFileIsNotValidJson_ReplacesItRatherThanFailing()
    {
        await File.WriteAllTextAsync(SettingsPath, "{ this is not json");

        await _store.SaveAsync(Defaults() with { Hotkey = ConfiguredChord }, CancellationToken.None);

        Bind<ShellOptions>(ShellOptions.SectionName).Hotkey.Should().Be(ConfiguredChord);
    }

    [Fact]
    public async Task SaveAsync_AfterWriting_LeavesNoTemporaryFileBehind()
    {
        await _store.SaveAsync(Defaults(), CancellationToken.None);

        Directory.GetFiles(_directory).Should().ContainSingle();
    }

    [Fact]
    public async Task SaveAsync_AfterWriting_ProducesReadableJson()
    {
        await _store.SaveAsync(Defaults(), CancellationToken.None);

        var text = await File.ReadAllTextAsync(SettingsPath);
        var act = () => JsonDocument.Parse(text);

        act.Should().NotThrow();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }

    private TOptions Bind<TOptions>(string sectionName)
        where TOptions : new()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(SettingsPath, optional: false, reloadOnChange: false)
            .Build();

        var options = new TOptions();
        configuration.GetSection(sectionName).Bind(options);

        return options;
    }

    private static FinderSettings Defaults() =>
        FinderSettings.From(new ShellOptions(), new SearchOptions(), new LogLevelOptions(), new RecapOptions());

    private sealed class StubPaths(string settingsFilePath) : IApplicationPaths
    {
        public string SettingsFilePath => settingsFilePath;

        public string IndexFilePath => @"C:\index\index.db";

        public string LogFolderPath => @"C:\logs";
    }
}
