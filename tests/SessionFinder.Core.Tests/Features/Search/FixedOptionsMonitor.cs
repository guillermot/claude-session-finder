using Microsoft.Extensions.Options;

namespace SessionFinder.Core.Tests.Features.Search;

/// <summary>
/// An options monitor over a value that never changes, so a handler that is written for hot
/// reload can be tested without a configuration provider behind it.
/// </summary>
internal sealed class FixedOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
    public TOptions CurrentValue => value;

    public TOptions Get(string? name) => value;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
