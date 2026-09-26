using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class RevealInFileExplorerHandlerTests
{
    private readonly StubFileManagerLocator _fileManager = new();
    private readonly RecordingShellLauncher _launcher = new();

    [Fact]
    public async Task HandleAsync_AKnownFolder_AsksTheFileManagerToShowIt()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Reveal(@"C:\git\my repo"), CancellationToken.None);

        _launcher.Last!.Arguments.Should().Equal(@"C:\git\my repo");
    }

    [Fact]
    public async Task HandleAsync_AFolderContainingASpace_QuotesItOnTheCommandLine()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Reveal(@"C:\git\my repo"), CancellationToken.None);

        _launcher.Last!.ToArgumentString().Should().Be(@"""C:\git\my repo""");
    }

    [Fact]
    public async Task HandleAsync_AKnownFolder_LeavesTheFileManagersExitCodeAlone()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Reveal(@"C:\git\repo"), CancellationToken.None);

        _launcher.Last!.IsExitCodeMeaningful.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_AnUnknownFolder_FailsWithoutLaunchingAnything()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new RevealInFileExplorerCommand(WorkingFolder.Unknown),
            CancellationToken.None);

        result.Error.Should().Be(AppError.FolderUnknown);
        _launcher.Launched.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AKnownFolder_Succeeds()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Reveal(@"C:\git\repo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AFileManagerWithLeadingArguments_PutsThemBeforeTheFolder()
    {
        _fileManager.Finds(new LaunchProgram
        {
            Executable = "/usr/bin/open",
            LeadingArguments = ["-R"],
        });
        var handler = CreateHandler();

        await handler.HandleAsync(Reveal("/Users/someone/git/repo"), CancellationToken.None);

        _launcher.Last!.Arguments.Should().Equal("-R", "/Users/someone/git/repo");
    }

    [Fact]
    public async Task HandleAsync_NoFileManager_SaysSoInsteadOfLaunchingAnything()
    {
        _fileManager.FindsNothing();
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Reveal(@"C:\git\repo"), CancellationToken.None);

        result.Error.Should().Be(AppError.FileManagerNotFound);
        _launcher.Launched.Should().BeEmpty();
    }

    private static RevealInFileExplorerCommand Reveal(string folder) =>
        new(WorkingFolder.FromTranscriptCwd(folder));

    private RevealInFileExplorerHandler CreateHandler() => new(_fileManager, _launcher);
}
