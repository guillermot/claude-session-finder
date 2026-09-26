using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class ShellCommandTests
{
    [Fact]
    public void ToArgumentString_AnArgumentWithoutSpaces_LeavesItUnquoted()
    {
        var command = Command("--resume", "abc");

        command.ToArgumentString().Should().Be("--resume abc");
    }

    [Fact]
    public void ToArgumentString_APathContainingASpace_QuotesIt()
    {
        var command = Command("-d", @"C:\git\my repo");

        command.ToArgumentString().Should().Be(@"-d ""C:\git\my repo""");
    }

    [Fact]
    public void ToArgumentString_APathEndingInASeparator_DoublesItSoTheQuoteSurvives()
    {
        var command = Command("-d", @"C:\my repo\");

        command.ToArgumentString().Should().Be(@"-d ""C:\my repo\\""");
    }

    [Fact]
    public void ToArgumentString_AnArgumentContainingAQuote_EscapesIt()
    {
        var command = Command(@"say ""hello""");

        command.ToArgumentString().Should().Be(@"""say \""hello\""""");
    }

    [Fact]
    public void ToArgumentString_AnEmptyArgument_BecomesAnEmptyQuotedPair()
    {
        var command = Command(string.Empty);

        command.ToArgumentString().Should().Be(@"""""");
    }

    [Fact]
    public void ToArgumentString_NoArguments_IsEmpty()
    {
        var command = Command();

        command.ToArgumentString().Should().BeEmpty();
    }

    [Fact]
    public void ToArgumentString_BackslashesNotTouchingAQuote_AreLeftAlone()
    {
        var command = Command(@"C:\git\my repo\src\file");

        command.ToArgumentString().Should().Be(@"""C:\git\my repo\src\file""");
    }

    [Fact]
    public void ToCommandLine_AnExecutableContainingASpace_QuotesTheExecutableToo()
    {
        var command = new ShellCommand
        {
            Executable = @"C:\Program Files\Editor\Code.exe",
            Arguments = [@"C:\git\my repo"],
        };

        command.ToCommandLine().Should().Be(@"""C:\Program Files\Editor\Code.exe"" ""C:\git\my repo""");
    }

    [Fact]
    public void ToCommandLine_NoArguments_IsJustTheExecutable()
    {
        var command = new ShellCommand { Executable = "explorer.exe", Arguments = [] };

        command.ToCommandLine().Should().Be("explorer.exe");
    }

    private static ShellCommand Command(params string[] arguments) => new()
    {
        Executable = "program.exe",
        Arguments = arguments,
    };
}
