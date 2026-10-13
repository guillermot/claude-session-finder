using SessionFinder.Presentation.Recap;

namespace SessionFinder.Presentation.Tests.Recap;

public sealed class MarkdownLinesTests
{
    [Fact]
    public void Parse_ABoldOnlyLine_IsAHeading()
    {
        var line = MarkdownLines.Parse("**Sat, Sep 26**").Single();

        line.IsHeading.Should().BeTrue();
        line.Spans.Should().ContainSingle().Which.Text.Should().Be("Sat, Sep 26");
    }

    [Fact]
    public void Parse_AHashHeading_IsAHeadingWithoutTheHashes()
    {
        var line = MarkdownLines.Parse("## Yesterday").Single();

        line.IsHeading.Should().BeTrue();
        line.Spans.Single().Text.Should().Be("Yesterday");
    }

    [Fact]
    public void Parse_NestedBullets_CarryTheirDepth()
    {
        var lines = MarkdownLines.Parse("- project\n  - session\n    - `824d14a` commit");

        lines.Select(line => line.Indent).Should().Equal(0, 1, 2);
        lines.Should().OnlyContain(line => line.IsBullet);
    }

    [Fact]
    public void Parse_MixedStyles_SplitsIntoSpans()
    {
        var line = MarkdownLines.Parse("- **finder** · `main` · 2 sessions").Single();

        line.Spans.Should().Equal(
            new MarkdownSpan("finder", IsBold: true, IsCode: false),
            new MarkdownSpan(" · ", IsBold: false, IsCode: false),
            new MarkdownSpan("main", IsBold: false, IsCode: true),
            new MarkdownSpan(" · 2 sessions", IsBold: false, IsCode: false));
    }

    [Fact]
    public void Parse_AsterisksInsideCode_AreNotBold()
    {
        var line = MarkdownLines.Parse("`a**b`").Single();

        line.Spans.Should().ContainSingle().Which.Should().Be(new MarkdownSpan("a**b", IsBold: false, IsCode: true));
    }

    [Fact]
    public void Parse_ABlankLine_IsKeptAsSpace()
    {
        var lines = MarkdownLines.Parse("**Today**\n\n**Earlier**");

        lines.Should().HaveCount(3);
        lines[1].IsBlank.Should().BeTrue();
    }

    [Fact]
    public void Parse_AnUnclosedMarker_KeepsTheText()
    {
        var line = MarkdownLines.Parse("a **dangling marker").Single();

        string.Concat(line.Spans.Select(span => span.Text)).Should().Be("a dangling marker");
    }

    [Theory]
    [InlineData("**Earlier**", RecapLineRole.Section)]
    [InlineData("**Yesterday**", RecapLineRole.Heading)]
    [InlineData("- **Fri, Sep 25**", RecapLineRole.Heading)]
    [InlineData("- **finder** · `main` · 2 sessions · 10:05–17:40", RecapLineRole.Project)]
    [InlineData("  - Path tests (12 prompts)", RecapLineRole.Item)]
    [InlineData("- Shipped the release", RecapLineRole.Item)]
    [InlineData("_No Claude Code activity on this day._", RecapLineRole.Text)]
    [InlineData("", RecapLineRole.Blank)]
    public void Role_EachKindOfRecapLine_IsRecognised(string markdown, RecapLineRole expected)
    {
        var line = MarkdownLines.Parse(markdown + "\nx")[0];

        line.Role.Should().Be(expected);
    }
}
