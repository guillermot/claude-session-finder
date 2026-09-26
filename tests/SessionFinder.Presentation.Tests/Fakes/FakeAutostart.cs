using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// A start-at-sign-in registration held in memory, which is how "the registry disagrees with the
/// setting" is reproduced without writing to the registry.
/// </summary>
internal sealed class FakeAutostart(bool isEnabled = false) : IAutostart
{
    private AppError? _failure;

    public bool IsEnabled { get; private set; } = isEnabled;

    public List<bool> Writes { get; } = [];

    public void Refuses(AppError error) => _failure = error;

    public Result<bool> ReadIsEnabled() =>
        _failure is null ? Result<bool>.Success(IsEnabled) : Result<bool>.Failure(_failure);

    public Result Apply(bool shouldStartWithSession)
    {
        if (_failure is not null)
        {
            return Result.Failure(_failure);
        }

        Writes.Add(shouldStartWithSession);
        IsEnabled = shouldStartWithSession;

        return Result.Success();
    }
}
