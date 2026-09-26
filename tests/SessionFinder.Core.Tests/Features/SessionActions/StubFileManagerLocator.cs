using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

/// <summary>
/// A file manager locator that answers whatever the test told it to.
/// </summary>
internal sealed class StubFileManagerLocator : IFileManagerLocator
{
    private Result<LaunchProgram> _answer =
        Result<LaunchProgram>.Success(new LaunchProgram { Executable = "explorer.exe" });

    public void Finds(LaunchProgram program) => _answer = Result<LaunchProgram>.Success(program);

    public void FindsNothing() => _answer = Result<LaunchProgram>.Failure(AppError.FileManagerNotFound);

    public Result<LaunchProgram> Locate() => _answer;
}
