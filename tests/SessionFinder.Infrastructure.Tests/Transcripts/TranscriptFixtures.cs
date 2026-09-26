using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Infrastructure.Tests.Transcripts;

/// <summary>
/// Access to the versioned transcript fixtures. Each file is one hazard measured on real
/// transcripts, so the set only ever grows.
/// </summary>
internal static class TranscriptFixtures
{
    public const string ContentAsPlainString = "01-content-as-plain-string.jsonl";
    public const string UserRecordWithToolResult = "02-user-record-with-tool-result.jsonl";
    public const string IgnoredRecordTypes = "03-ignored-record-types.jsonl";
    public const string TitlesAppendedAfterMessages = "04-titles-appended-after-messages.jsonl";
    public const string MixedCwdCasing = "05-mixed-cwd-casing.jsonl";
    public const string SingleHugeLine = "06-single-huge-line.jsonl";
    public const string TruncatedLastLine = "07-truncated-last-line.jsonl";
    public const string SpanishAccents = "08-spanish-accents.jsonl";
    public const string RootUserAfterSystem = "09-root-user-after-system.jsonl";
    public const string CrlfAndByteOrderMark = "10-crlf-lf-and-byte-order-mark.jsonl";

    private const string FolderName = "TestData";
    private const string SessionIdentifier = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";

    /// <summary>Every fixture whose last line is newline-terminated.</summary>
    public static TheoryData<string> Complete =>
    [
        ContentAsPlainString,
        UserRecordWithToolResult,
        IgnoredRecordTypes,
        TitlesAppendedAfterMessages,
        MixedCwdCasing,
        SingleHugeLine,
        SpanishAccents,
        RootUserAfterSystem,
        CrlfAndByteOrderMark,
    ];

    /// <summary>Absolute path of a fixture in the test output folder.</summary>
    public static string PathOf(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, FolderName, fixtureName);

    /// <summary>Length of a fixture in bytes.</summary>
    public static long LengthOf(string fixtureName) => new FileInfo(PathOf(fixtureName)).Length;

    /// <summary>All bytes of a fixture.</summary>
    public static byte[] BytesOf(string fixtureName) => File.ReadAllBytes(PathOf(fixtureName));

    /// <summary>Parses a whole fixture from offset zero.</summary>
    public static async Task<SessionDocument> ParseAsync(string fixtureName)
    {
        await using var stream = File.OpenRead(PathOf(fixtureName));

        return await ParseAsync(BuildRequest(fixtureName, stream), CancellationToken.None);
    }

    /// <summary>Parses an arbitrary request with the production parser.</summary>
    public static Task<SessionDocument> ParseAsync(TranscriptParseRequest request, CancellationToken cancellationToken) =>
        new JsonlSessionTranscriptParser().ParseAsync(request, cancellationToken);

    /// <summary>Builds a full-pass request for a fixture.</summary>
    public static TranscriptParseRequest BuildRequest(string fixtureName, Stream content) => new()
    {
        SessionId = ParseIdentifier(),
        FileName = fixtureName,
        Content = content,
    };

    /// <summary>The identifier every fixture declares, so documents can be compared.</summary>
    public static SessionId ParseIdentifier()
    {
        SessionId.TryParseFromFileName(SessionIdentifier, out var sessionId);

        return sessionId;
    }
}
