using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class WindowsTerminalCommandFactoryTests
{
    private static readonly WindowsTerminalCommandFactory Factory = new();
    private static readonly SessionId Session = new(new Guid("2f2c9b5a-5a6d-4f5e-9d1e-0c6f0a1b2c3d"));
    private static readonly WorkingFolder FolderWithSpace =
        WorkingFolder.FromTranscriptCwd(@"C:\git\my repo");

    [Fact]
    public void CreateResumeCommand_ATerminalHost_PassesTheFolderAsItsDirectoryArgument()
    {
        var command = Factory.CreateResumeCommand(TerminalHost(), FolderWithSpace, Session);

        command.Arguments.Should().StartWith(["-d", @"C:\git\my repo"]);
    }

    [Fact]
    public void CreateResumeCommand_ATerminalHost_RunsTheResumeThroughAShellThatStaysOpen()
    {
        var command = Factory.CreateResumeCommand(TerminalHost(), FolderWithSpace, Session);

        command.Arguments.Should().EndWith(["cmd", "/k", "claude", "--resume", Session.ToString()]);
    }

    [Fact]
    public void CreateResumeCommand_ATerminalHost_QuotesAFolderContainingASpace()
    {
        var command = Factory.CreateResumeCommand(TerminalHost(), FolderWithSpace, Session);

        command.ToArgumentString().Should()
            .Be($@"-d ""C:\git\my repo"" cmd /k claude --resume {Session}");
    }

    [Fact]
    public void CreateResumeCommand_ATerminalHost_NeedsNoConsoleWindowOfItsOwn()
    {
        var command = Factory.CreateResumeCommand(TerminalHost(), FolderWithSpace, Session);

        command.ConsoleWindow.Should().Be(ConsoleWindowMode.None);
    }

    [Fact]
    public void CreateResumeCommand_ACommandShell_StartsInTheFolderBecauseItHasNoDirectoryArgument()
    {
        var command = Factory.CreateResumeCommand(CommandShell(), FolderWithSpace, Session);

        command.WorkingDirectory.Should().Be(@"C:\git\my repo");
    }

    [Fact]
    public void CreateResumeCommand_ACommandShell_KeepsTheShellOpenAfterTheResume()
    {
        var command = Factory.CreateResumeCommand(CommandShell(), FolderWithSpace, Session);

        command.Arguments.Should().Equal("/k", "claude", "--resume", Session.ToString());
    }

    [Fact]
    public void CreateResumeCommand_ACommandShell_AsksForAVisibleConsoleWindow()
    {
        var command = Factory.CreateResumeCommand(CommandShell(), FolderWithSpace, Session);

        command.ConsoleWindow.Should().Be(ConsoleWindowMode.Visible);
    }

    [Fact]
    public void CreateResumeCommand_AnyTerminal_CarriesTheFolderTheSessionWasStartedIn()
    {
        var command = Factory.CreateResumeCommand(TerminalHost(), FolderWithSpace, Session);

        command.WorkingDirectory.Should().Be(@"C:\git\my repo");
    }

    [Fact]
    public void BuildResumeCommandLine_AFolderContainingASpace_QuotesItInTheChangeOfDirectory()
    {
        var line = Factory.BuildResumeCommandLine(FolderWithSpace, Session);

        line.Should().Be($@"cd /d ""C:\git\my repo"" && claude --resume {Session}");
    }

    [Fact]
    public void BuildResumeCommandLine_APlainFolder_LeavesItUnquoted()
    {
        var folder = WorkingFolder.FromTranscriptCwd(@"C:\git\Project");

        var line = Factory.BuildResumeCommandLine(folder, Session);

        line.Should().Be($@"cd /d C:\git\Project && claude --resume {Session}");
    }

    private static TerminalProgram TerminalHost() => new()
    {
        Executable = @"C:\Terminal\wt.exe",
        Kind = TerminalKind.TerminalHost,
    };

    private static TerminalProgram CommandShell() => new()
    {
        Executable = @"C:\Windows\System32\cmd.exe",
        Kind = TerminalKind.CommandShell,
    };
}
