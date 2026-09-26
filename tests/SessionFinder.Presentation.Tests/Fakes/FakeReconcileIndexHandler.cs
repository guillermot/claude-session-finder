using SessionFinder.Core.Features.ReconcileIndex;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// Records how the index was asked to be brought up to date, and can be told to fail the way a
/// pass over a disk that has gone away fails.
/// </summary>
internal sealed class FakeReconcileIndexHandler : IReconcileIndexHandler
{
    private Exception? _failure;

    public List<ReconcileIndexCommand> Commands { get; } = [];

    public void Throws(Exception exception) => _failure = exception;

    public Task<ReconcileIndexResult> HandleAsync(
        ReconcileIndexCommand command,
        CancellationToken cancellationToken)
    {
        Commands.Add(command);

        if (_failure is not null)
        {
            return Task.FromException<ReconcileIndexResult>(_failure);
        }

        return Task.FromResult(new ReconcileIndexResult
        {
            FilesDiscovered = 3,
            SessionsIndexed = 3,
            SessionsSkipped = 0,
            SessionsPruned = 0,
            ChunksWritten = 12,
            BytesRead = 4096,
            SessionsWithParseErrors = 0,
            FilesFailed = 0,
            Elapsed = TimeSpan.FromSeconds(2),
        });
    }
}
