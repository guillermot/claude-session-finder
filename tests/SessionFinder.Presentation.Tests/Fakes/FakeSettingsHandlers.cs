using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.Settings;
using SessionFinder.Core.Results;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// Stands in for both halves of the settings slice, recording what the window asked to store.
/// </summary>
internal sealed class FakeSettingsHandlers : IGetSettingsHandler, IUpdateSettingsHandler
{
    private const string IndexPath = @"C:\index\index.db";
    private const string SettingsPath = @"C:\settings\settings.json";
    private const string LogFolder = @"C:\logs";

    private Result _answer = Result.Success();

    public FinderSettings Stored { get; private set; } = Defaults();

    public List<FinderSettings> Saves { get; } = [];

    public void Holds(FinderSettings settings) => Stored = settings;

    public void Refuses(AppError error) => _answer = Result.Failure(error);

    public Task<GetSettingsResult> HandleAsync(GetSettingsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new GetSettingsResult(Stored, SettingsPath, IndexPath, LogFolder));

    public Task<Result> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        Saves.Add(command.Settings);

        return Task.FromResult(_answer);
    }

    public static FinderSettings Defaults() => FinderSettings.From(
        new ShellOptions(),
        new SearchOptions(),
        new LogLevelOptions(),
        new RecapOptions());
}
