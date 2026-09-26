using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class ResumeSessionHandlerTests
{
    private static readonly SessionId Session = new(new Guid("6b1f6d7e-1a2b-4c3d-8e9f-0a1b2c3d4e5f"));

    private readonly StubTerminalLocator _locator = new();
    private readonly RecordingShellLauncher _launcher = new();

    [Fact]
    public async Task HandleAsync_AKnownFolder_LaunchesTheResumeInThatFolder()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Resume(@"C:\git\my repo"), CancellationToken.None);

        _launcher.Last!.WorkingDirectory.Should().Be(@"C:\git\my repo");
    }

    [Fact]
    public async Task HandleAsync_AKnownFolder_PassesTheSessionIdentifierToTheResume()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(Resume(@"C:\git\repo"), CancellationToken.None);

        _launcher.Last!.Arguments.Should().Contain(Session.ToString());
    }

    [Fact]
    public async Task HandleAsync_AnUnknownFolder_FailsWithoutLaunchingAnything()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new ResumeSessionCommand(Session, WorkingFolder.Unknown),
            CancellationToken.None);

        result.Error.Should().Be(AppError.FolderUnknown);
        _launcher.Launched.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NoTerminalInstalled_SaysSoInsteadOfThrowing()
    {
        _locator.FindsNothing();
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Resume(@"C:\git\repo"), CancellationToken.None);

        result.Error.Should().Be(AppError.TerminalNotFound);
    }

    [Fact]
    public async Task HandleAsync_ACommandShellTerminal_AsksForAVisibleConsoleWindow()
    {
        _locator.Finds(new TerminalProgram
        {
            Executable = @"C:\Windows\System32\cmd.exe",
            Kind = TerminalKind.CommandShell,
        });
        var handler = CreateHandler();

        await handler.HandleAsync(Resume(@"C:\git\repo"), CancellationToken.None);

        _launcher.Last!.ConsoleWindow.Should().Be(ConsoleWindowMode.Visible);
    }

    [Fact]
    public async Task HandleAsync_AKnownFolder_Succeeds()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Resume(@"C:\git\repo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static ResumeSessionCommand Resume(string folder) =>
        new(Session, WorkingFolder.FromTranscriptCwd(folder));

    private ResumeSessionHandler CreateHandler() =>
        new(_locator, new WindowsTerminalCommandFactory(), _launcher);
}
