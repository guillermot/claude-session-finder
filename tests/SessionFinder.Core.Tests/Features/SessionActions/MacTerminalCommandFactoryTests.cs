using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class MacTerminalCommandFactoryTests
{
    private static readonly MacTerminalCommandFactory Factory = new();
    private static readonly SessionId Session = new(new Guid("2f2c9b5a-5a6d-4f5e-9d1e-0c6f0a1b2c3d"));
    private static readonly WorkingFolder FolderWithSpace =
        WorkingFolder.FromTranscriptCwd("/Users/someone/my repo");

    [Fact]
    public void CreateResumeCommand_AnyTerminal_RunsOsaScriptRatherThanTheTerminalItself()
    {
        var command = Factory.CreateResumeCommand(TerminalApp(), FolderWithSpace, Session);

        command.Executable.Should().Be("/usr/bin/osascript");
    }

    [Fact]
    public void CreateResumeCommand_TerminalApp_TypesTheResumeIntoAShellThatStaysBehind()
    {
        var command = Factory.CreateResumeCommand(TerminalApp(), FolderWithSpace, Session);

        command.Arguments.Should().Equal(
            "-e",
            "tell application \"Terminal\" to do script "
                + $"\"cd '/Users/someone/my repo' && claude --resume {Session}\"",
            "-e",
            "tell application \"Terminal\" to activate");
    }

    [Fact]
    public void CreateResumeCommand_ITerm_WritesTheResumeIntoANewWindowsSession()
    {
        var command = Factory.CreateResumeCommand(ITerm(), FolderWithSpace, Session);

        command.Arguments.Should().Equal(
            "-e",
            "tell application \"iTerm\"",
            "-e",
            "activate",
            "-e",
            "set newWindow to (create window with default profile)",
            "-e",
            "tell current session of newWindow to write text "
                + $"\"cd '/Users/someone/my repo' && claude --resume {Session}\"",
            "-e",
            "end tell");
    }

    [Fact]
    public void CreateResumeCommand_AFolderContainingAnApostrophe_EscapesTheBackslashForAppleScript()
    {
        var folder = WorkingFolder.FromTranscriptCwd("/Users/someone/tom's repo");

        var command = Factory.CreateResumeCommand(TerminalApp(), folder, Session);

        command.Arguments[1].Should().Contain(@"cd '/Users/someone/tom'\\''s repo'");
    }

    [Fact]
    public void CreateResumeCommand_AnyTerminal_NeedsNoConsoleWindowOfItsOwn()
    {
        var command = Factory.CreateResumeCommand(TerminalApp(), FolderWithSpace, Session);

        command.ConsoleWindow.Should().Be(ConsoleWindowMode.None);
    }

    /// <summary>
    /// osascript is the one program here whose exit code is worth reading. It exits straight away
    /// on success, having handed the line to the terminal; a nonzero code within that same moment
    /// is the terminal not being there at all, which the user would otherwise never be told.
    /// </summary>
    [Fact]
    public void CreateResumeCommand_AnyTerminal_AsksForItsExitCodeToBeRead()
    {
        var command = Factory.CreateResumeCommand(TerminalApp(), FolderWithSpace, Session);

        command.IsExitCodeMeaningful.Should().BeTrue();
    }

    /// <summary>
    /// Unlike the Windows commands, nothing is started in the folder: osascript runs wherever the
    /// launcher does, and the change of directory travels inside the line the terminal is given.
    /// </summary>
    [Fact]
    public void CreateResumeCommand_AnyTerminal_CarriesTheFolderInTheLineRatherThanAsAWorkingDirectory()
    {
        var command = Factory.CreateResumeCommand(TerminalApp(), FolderWithSpace, Session);

        command.WorkingDirectory.Should().BeNull();
        command.Arguments[1].Should().Contain("cd '/Users/someone/my repo'");
    }

    [Fact]
    public void BuildResumeCommandLine_AFolderContainingASpace_QuotesItInTheChangeOfDirectory()
    {
        var line = Factory.BuildResumeCommandLine(FolderWithSpace, Session);

        line.Should().Be($"cd '/Users/someone/my repo' && claude --resume {Session}");
    }

    /// <summary>
    /// The Windows line carries <c>/d</c> so that the change of directory crosses drives. macOS has
    /// one root, and the flag would be read as an argument rather than ignored.
    /// </summary>
    [Fact]
    public void BuildResumeCommandLine_AnyFolder_LeavesOutTheChangeDriveFlag()
    {
        var line = Factory.BuildResumeCommandLine(FolderWithSpace, Session);

        line.Should().NotContain("/d");
    }

    private static TerminalProgram TerminalApp() => new()
    {
        Executable = "Terminal",
        Kind = TerminalKind.MacTerminalApp,
    };

    private static TerminalProgram ITerm() => new()
    {
        Executable = "iTerm",
        Kind = TerminalKind.MacITerm,
    };
}
