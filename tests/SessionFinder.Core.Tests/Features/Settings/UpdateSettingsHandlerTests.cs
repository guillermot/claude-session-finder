using Microsoft.Extensions.Logging.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.Settings;

namespace SessionFinder.Core.Tests.Features.Settings;

public sealed class UpdateSettingsHandlerTests
{
    private readonly RecordingSettingsStore _store = new();

    [Fact]
    public async Task HandleAsync_TheSettingsAreSound_WritesThem()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Command(Defaults()), CancellationToken.None);

        _store.Saves.Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_TheSettingsAreSound_Succeeds()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Command(Defaults()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ASettingIsOutOfRange_WritesNothing()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Command(Defaults() with { MaxResults = 0 }), CancellationToken.None);

        _store.Saves.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ASettingIsOutOfRange_SaysWhichOne()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            Command(Defaults() with { MaxResults = 0 }),
            CancellationToken.None);

        result.Error!.Message.Should().Contain(nameof(FinderSettings.MaxResults));
    }

    [Fact]
    public async Task HandleAsync_TheFileCannotBeWritten_ReportsItInsteadOfThrowing()
    {
        _store.Throws(new IOException("the file is in use"));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Command(Defaults()), CancellationToken.None);

        result.Error!.Message.Should().Contain("the file is in use");
    }

    private UpdateSettingsHandler CreateHandler() =>
        new(_store, NullLogger<UpdateSettingsHandler>.Instance);

    private static UpdateSettingsCommand Command(FinderSettings settings) => new() { Settings = settings };

    private static FinderSettings Defaults() =>
        FinderSettings.From(new ShellOptions(), new SearchOptions(), new LogLevelOptions());
}
