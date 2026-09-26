using SessionFinder.Core.Features.Settings;

namespace SessionFinder.Core.Tests.Features.Settings;

/// <summary>
/// Records what was written, and can be told to fail the way a settings file on a full disk or
/// held open by another program fails.
/// </summary>
internal sealed class RecordingSettingsStore : ISettingsStore
{
    private Exception? _failure;

    public List<FinderSettings> Saves { get; } = [];

    public void Throws(Exception exception) => _failure = exception;

    public Task SaveAsync(FinderSettings settings, CancellationToken cancellationToken)
    {
        if (_failure is not null)
        {
            return Task.FromException(_failure);
        }

        Saves.Add(settings);

        return Task.CompletedTask;
    }
}
