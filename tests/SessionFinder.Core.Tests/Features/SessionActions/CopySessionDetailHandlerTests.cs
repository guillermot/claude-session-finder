using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class CopySessionDetailHandlerTests
{
    private static readonly SessionId Session = new(new Guid("8d3e1c42-7b5a-4e6f-9a0b-1c2d3e4f5a6b"));

    private readonly RecordingClipboardService _clipboard = new();

    [Fact]
    public async Task HandleAsync_TheFolderPath_CopiesItWithItsOriginalCasing()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(
            Copy(SessionDetail.FolderPath, @"C:\git\Project"),
            CancellationToken.None);

        _clipboard.Last.Should().Be(@"C:\git\Project");
    }

    [Fact]
    public async Task HandleAsync_TheSessionIdentifier_CopiesItOnItsOwn()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(
            Copy(SessionDetail.SessionId, @"C:\git\Project"),
            CancellationToken.None);

        _clipboard.Last.Should().Be(Session.ToString());
    }

    [Fact]
    public async Task HandleAsync_TheSessionIdentifierOfASessionWithNoFolder_StillCopiesIt()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new CopySessionDetailCommand(SessionDetail.SessionId, Session, WorkingFolder.Unknown),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _clipboard.Last.Should().Be(Session.ToString());
    }

    [Fact]
    public async Task HandleAsync_TheResumeCommandLine_IncludesTheChangeOfDirectoryItNeeds()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(
            Copy(SessionDetail.ResumeCommandLine, @"C:\git\my repo"),
            CancellationToken.None);

        _clipboard.Last.Should().Be($@"cd /d ""C:\git\my repo"" && claude --resume {Session}");
    }

    [Fact]
    public async Task HandleAsync_TheFolderPathOfASessionWithNoFolder_FailsWithoutCopyingAnything()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new CopySessionDetailCommand(SessionDetail.FolderPath, Session, WorkingFolder.Unknown),
            CancellationToken.None);

        result.Error.Should().Be(AppError.FolderUnknown);
        _clipboard.Copied.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_TheResumeCommandLineOfASessionWithNoFolder_FailsWithoutCopyingAnything()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new CopySessionDetailCommand(SessionDetail.ResumeCommandLine, Session, WorkingFolder.Unknown),
            CancellationToken.None);

        result.Error.Should().Be(AppError.FolderUnknown);
        _clipboard.Copied.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AClipboardHeldByAnotherProcess_ReportsItRatherThanThrowing()
    {
        _clipboard.Fails(AppError.ClipboardUnavailable("the clipboard could not be opened"));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            Copy(SessionDetail.SessionId, @"C:\git\Project"),
            CancellationToken.None);

        result.Error!.Code.Should().Be(nameof(AppError.ClipboardUnavailable));
    }

    private static CopySessionDetailCommand Copy(SessionDetail detail, string folder) =>
        new(detail, Session, WorkingFolder.FromTranscriptCwd(folder));

    private CopySessionDetailHandler CreateHandler() =>
        new(_clipboard, new WindowsTerminalCommandFactory());
}
