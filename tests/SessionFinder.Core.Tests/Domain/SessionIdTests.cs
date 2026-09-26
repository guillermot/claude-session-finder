using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Domain;

/// <summary>
/// Covers the one fact this type centralises: the transcript file name is the session identifier.
/// </summary>
public sealed class SessionIdTests
{
    private const string Identifier = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";

    [Theory]
    [InlineData(Identifier)]
    [InlineData(Identifier + ".jsonl")]
    [InlineData(@"C:\Users\someone\.claude\projects\c--work-project\" + Identifier + ".jsonl")]
    [InlineData("/Users/someone/.claude/projects/c--work-project/" + Identifier + ".jsonl")]
    public void TryParseFromFileName_TranscriptNameOrPath_RecoversTheIdentifier(string input)
    {
        var parsed = SessionId.TryParseFromFileName(input, out var sessionId);

        parsed.Should().BeTrue();
        sessionId.ToString().Should().Be(Identifier);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("history.jsonl")]
    [InlineData("not-a-guid.jsonl")]
    [InlineData("{0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d}.jsonl")]
    public void TryParseFromFileName_AnythingElse_Fails(string? input)
    {
        var parsed = SessionId.TryParseFromFileName(input, out var sessionId);

        parsed.Should().BeFalse();
        sessionId.Should().Be(default(SessionId));
    }

    [Fact]
    public void ToString_OfAParsedIdentifier_RoundTrips()
    {
        SessionId.TryParseFromFileName(Identifier + ".jsonl", out var sessionId);

        SessionId.TryParseFromFileName(sessionId.ToString(), out var reparsed);

        reparsed.Should().Be(sessionId);
    }
}
