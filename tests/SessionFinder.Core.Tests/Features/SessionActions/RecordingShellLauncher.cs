using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

/// <summary>
/// A launcher that keeps what it was asked to run instead of running it, which is how a test reads
/// the command a handler built.
/// </summary>
internal sealed class RecordingShellLauncher : IShellLauncher
{
    private Result _answer = Result.Success();

    public List<ShellCommand> Launched { get; } = [];

    public ShellCommand? Last => Launched.Count == 0 ? null : Launched[^1];

    public void Fails(AppError error) => _answer = Result.Failure(error);

    public Task<Result> LaunchAsync(ShellCommand command, CancellationToken cancellationToken)
    {
        Launched.Add(command);

        return Task.FromResult(_answer);
    }
}
