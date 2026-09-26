using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

/// <summary>
/// A terminal locator that answers whatever the test told it to.
/// </summary>
internal sealed class StubTerminalLocator : ITerminalLocator
{
    private Result<TerminalProgram> _answer = Result<TerminalProgram>.Success(
        new TerminalProgram { Executable = @"C:\Terminal\wt.exe", Kind = TerminalKind.TerminalHost });

    public void Finds(TerminalProgram program) => _answer = Result<TerminalProgram>.Success(program);

    public void FindsNothing() => _answer = Result<TerminalProgram>.Failure(AppError.TerminalNotFound);

    public Result<TerminalProgram> Locate() => _answer;
}
