using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Presentation.Tests.Fakes;

/// <summary>
/// One stand-in for all four action handlers, recording what it was asked to do and answering
/// however the test set it up.
/// </summary>
/// <remarks>
/// The four are faked together because the view model treats them identically: every command goes
/// through the same seam, and what a test wants to say is "the handler refused" rather than which
/// of four interfaces refused.
/// </remarks>
internal sealed class FakeSessionActionHandlers
    : IOpenFolderInEditorHandler,
        IRevealInFileExplorerHandler,
        IResumeSessionHandler,
        ICopySessionDetailHandler
{
    private Result _answer = Result.Success();
    private Exception? _failure;

    public List<string> Calls { get; } = [];

    public void Refuses(AppError error) => _answer = Result.Failure(error);

    public void Throws(Exception exception) => _failure = exception;

    public Task<Result> HandleAsync(OpenFolderInEditorCommand command, CancellationToken cancellationToken) =>
        Record(nameof(OpenFolderInEditorCommand));

    public Task<Result> HandleAsync(RevealInFileExplorerCommand command, CancellationToken cancellationToken) =>
        Record(nameof(RevealInFileExplorerCommand));

    public Task<Result> HandleAsync(ResumeSessionCommand command, CancellationToken cancellationToken) =>
        Record(nameof(ResumeSessionCommand));

    public Task<Result> HandleAsync(CopySessionDetailCommand command, CancellationToken cancellationToken) =>
        Record($"{nameof(CopySessionDetailCommand)}:{command.Detail}");

    private Task<Result> Record(string call)
    {
        Calls.Add(call);

        return _failure is null ? Task.FromResult(_answer) : Task.FromException<Result>(_failure);
    }
}
