using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class OpenFolderInEditorHandlerTests
{
    private readonly StubEditorLocator _locator = new();
    private readonly RecordingShellLauncher _launcher = new();

    [Fact]
    public async Task HandleAsync_AKnownFolder_PassesItToTheEditorAsTheLastArgument()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Open(@"C:\git\my repo"), CancellationToken.None);

        _launcher.Last!.Arguments.Should().Equal(@"C:\git\my repo");
    }

    [Fact]
    public async Task HandleAsync_AnEditorReachedThroughAShell_KeepsTheShellArgumentsInFront()
    {
        _locator.Finds(new LaunchProgram
        {
            Executable = "cmd.exe",
            LeadingArguments = ["/c", "code"],
            ConsoleWindow = ConsoleWindowMode.Suppressed,
        });
        var handler = CreateHandler();

        await handler.HandleAsync(Open(@"C:\git\my repo"), CancellationToken.None);

        _launcher.Last!.Arguments.Should().Equal("/c", "code", @"C:\git\my repo");
    }

    [Fact]
    public async Task HandleAsync_AnEditorReachedThroughAShell_SuppressesTheConsoleWindow()
    {
        _locator.Finds(new LaunchProgram
        {
            Executable = "cmd.exe",
            LeadingArguments = ["/c", "code"],
            ConsoleWindow = ConsoleWindowMode.Suppressed,
        });
        var handler = CreateHandler();

        await handler.HandleAsync(Open(@"C:\git\repo"), CancellationToken.None);

        _launcher.Last!.ConsoleWindow.Should().Be(ConsoleWindowMode.Suppressed);
    }

    [Fact]
    public async Task HandleAsync_AKnownFolder_AsksForTheEditorsExitCodeToBeRead()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Open(@"C:\git\repo"), CancellationToken.None);

        _launcher.Last!.IsExitCodeMeaningful.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AnUnknownFolder_FailsWithoutLaunchingAnything()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new OpenFolderInEditorCommand(WorkingFolder.Unknown),
            CancellationToken.None);

        result.Error.Should().Be(AppError.FolderUnknown);
        _launcher.Launched.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NoEditorInstalled_SaysSoInsteadOfThrowing()
    {
        _locator.FindsNothing();
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            Open(@"C:\git\repo"),
            CancellationToken.None);

        result.Error.Should().Be(AppError.EditorNotFound);
    }

    [Fact]
    public async Task HandleAsync_ALauncherThatRefuses_ReportsWhyRatherThanClaimingSuccess()
    {
        _launcher.Fails(AppError.LaunchFailed("Code.exe", "the system cannot find the file specified"));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            Open(@"C:\git\repo"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AKnownFolder_Succeeds()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            Open(@"C:\git\repo"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static OpenFolderInEditorCommand Open(string folder) =>
        new(WorkingFolder.FromTranscriptCwd(folder));

    private OpenFolderInEditorHandler CreateHandler() => new(_locator, _launcher);
}
