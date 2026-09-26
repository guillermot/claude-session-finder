using System.Text;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Infrastructure.Tests.Transcripts;

/// <summary>
/// Covers the polymorphism of <c>message.content</c>, where the rare shape is the one that gets
/// dropped silently if it is not handled.
/// </summary>
public sealed class MessageContentReaderTests
{
    [Fact]
    public void TryReadTextBlocks_ContentIsAPlainString_YieldsThatString()
    {
        var blocks = Read("""{"role":"user","content":"a prompt written as a plain string"}""");

        blocks.Should().Equal("a prompt written as a plain string");
    }

    [Fact]
    public void TryReadTextBlocks_ContentIsAnArrayOfTextBlocks_YieldsEachOne()
    {
        var blocks = Read("""
            {"role":"assistant","content":[{"type":"text","text":"first"},{"type":"text","text":"second"}]}
            """);

        blocks.Should().Equal("first", "second");
    }

    [Theory]
    [InlineData("""{"type":"thinking","thinking":"internal reasoning","signature":"s"}""")]
    [InlineData("""{"type":"tool_use","id":"t1","name":"Bash","input":{"command":"ls"}}""")]
    [InlineData("""{"type":"tool_result","tool_use_id":"t1","content":"tool output"}""")]
    [InlineData("""{"type":"image","source":{"type":"base64","data":"AAAA"}}""")]
    public void TryReadTextBlocks_BlockIsNotText_YieldsNothing(string block)
    {
        var blocks = Read($$"""{"role":"assistant","content":[{{block}}]}""");

        blocks.Should().BeEmpty();
    }

    [Fact]
    public void TryReadTextBlocks_TextBlocksMixedWithToolTraffic_YieldsOnlyTheText()
    {
        var blocks = Read("""
            {"role":"assistant","content":[{"type":"thinking","thinking":"internal"},{"type":"text","text":"the answer"},{"type":"tool_use","id":"t1","name":"Bash","input":{}}]}
            """);

        blocks.Should().Equal("the answer");
    }

    [Fact]
    public void TryReadTextBlocks_TextPropertyBeforeTheTypeProperty_IsStillFound()
    {
        var blocks = Read("""{"role":"user","content":[{"text":"out of order","type":"text"}]}""");

        blocks.Should().Equal("out of order");
    }

    [Fact]
    public void TryReadTextBlocks_BlankText_IsDropped()
    {
        var blocks = Read("""{"role":"user","content":[{"type":"text","text":"   "}]}""");

        blocks.Should().BeEmpty();
    }

    [Fact]
    public void TryReadTextBlocks_NoContentProperty_YieldsNothing()
    {
        var blocks = Read("""{"role":"user","id":"msg_1"}""");

        blocks.Should().BeEmpty();
    }

    [Fact]
    public void TryReadTextBlocks_MalformedMessage_ReportsFailure()
    {
        var destination = new List<string>();

        var read = MessageContentReader.TryReadTextBlocks(Encoding.UTF8.GetBytes("""{"role":"user","conte"""), destination);

        read.Should().BeFalse();
    }

    private static List<string> Read(string messageJson)
    {
        var destination = new List<string>();

        MessageContentReader.TryReadTextBlocks(Encoding.UTF8.GetBytes(messageJson), destination).Should().BeTrue();

        return destination;
    }
}
