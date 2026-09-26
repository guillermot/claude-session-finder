using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

/// <summary>
/// An editor locator that answers whatever the test told it to.
/// </summary>
internal sealed class StubEditorLocator : IEditorLocator
{
    private Result<LaunchProgram> _answer =
        Result<LaunchProgram>.Success(new LaunchProgram { Executable = @"C:\Editor\Code.exe" });

    public void Finds(LaunchProgram program) => _answer = Result<LaunchProgram>.Success(program);

    public void FindsNothing() => _answer = Result<LaunchProgram>.Failure(AppError.EditorNotFound);

    public Result<LaunchProgram> Locate() => _answer;
}
