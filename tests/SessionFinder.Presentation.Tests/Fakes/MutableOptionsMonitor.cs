using Microsoft.Extensions.Options;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// An options monitor a test can change, which is how a settings file being rewritten while the
/// application runs is reproduced without a file.
/// </summary>
internal sealed class MutableOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
    private readonly List<Action<TOptions, string?>> _listeners = [];

    public TOptions CurrentValue { get; private set; } = value;

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<TOptions, string?> listener)
    {
        _listeners.Add(listener);

        return new Subscription(_listeners, listener);
    }

    /// <summary>Replaces the value and notifies every listener once.</summary>
    public void Set(TOptions updated) => Publish(updated, times: 1);

    /// <summary>
    /// Replaces the value and notifies every listener more than once, which is what a real
    /// configuration file watcher does for a single save.
    /// </summary>
    public void SetAndNotifyTwice(TOptions updated) => Publish(updated, times: 2);

    private void Publish(TOptions updated, int times)
    {
        CurrentValue = updated;

        for (var round = 0; round < times; round++)
        {
            foreach (var listener in _listeners.ToArray())
            {
                listener(updated, null);
            }
        }
    }

    private sealed class Subscription(List<Action<TOptions, string?>> listeners, Action<TOptions, string?> listener)
        : IDisposable
    {
        public void Dispose() => listeners.Remove(listener);
    }
}
