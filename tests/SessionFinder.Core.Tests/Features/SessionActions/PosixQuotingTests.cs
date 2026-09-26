using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class PosixQuotingTests
{
    [Fact]
    public void Quote_APlainPath_WrapsItInSingleQuotes()
    {
        PosixQuoting.Quote("/Users/someone/git/repo").Should().Be("'/Users/someone/git/repo'");
    }

    [Fact]
    public void Quote_APathContainingASpace_SurvivesAsOneArgument()
    {
        PosixQuoting.Quote("/Users/someone/my repo").Should().Be("'/Users/someone/my repo'");
    }

    [Fact]
    public void Quote_APathContainingAnApostrophe_ClosesAndReopensTheQuotedRun()
    {
        PosixQuoting.Quote("/Users/someone/tom's repo")
            .Should().Be(@"'/Users/someone/tom'\''s repo'");
    }

    [Fact]
    public void Quote_APathContainingADoubleQuote_LeavesItAloneBecauseSingleQuotesExpandNothing()
    {
        PosixQuoting.Quote(@"/Users/someone/say ""hi""")
            .Should().Be(@"'/Users/someone/say ""hi""'");
    }

    [Fact]
    public void Quote_APathContainingADollarSign_LeavesItUnexpanded()
    {
        PosixQuoting.Quote("/Users/someone/$HOME/repo").Should().Be("'/Users/someone/$HOME/repo'");
    }

    [Fact]
    public void Quote_AnEmptyArgument_StillProducesAnArgument()
    {
        PosixQuoting.Quote(string.Empty).Should().Be("''");
    }
}
