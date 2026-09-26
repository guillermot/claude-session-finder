using System.Text.Json;

namespace SessionFinder.Infrastructure.Tests.Indexing;

/// <summary>
/// Builds the transcript records the incremental tests append to a live file.
/// </summary>
/// <remarks>
/// The lines are serialised rather than written as string literals so that a working directory
/// full of backslashes is escaped by the same rules Claude escapes it by, instead of by whatever
/// the literal happened to say.
/// </remarks>
internal static class TranscriptLine
{
    public const string WorkingDirectory = @"C:\work\project";
    public const string GitBranch = "main";

    private const string ClientVersion = "2.0.0";
    private const string UserType = "external";
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";
    private const string TextBlockType = "text";
    private const string AssistantModel = "test-model";

    /// <summary>The first prompt of a session, which is the record carrying the working directory.</summary>
    public static string RootPrompt(string sessionId, string uuid, string text, DateTimeOffset timestamp) =>
        Serialize(new
        {
            parentUuid = (string?)null,
            isSidechain = false,
            type = "user",
            message = new { role = UserRole, content = text },
            uuid,
            timestamp,
            userType = UserType,
            cwd = WorkingDirectory,
            sessionId,
            version = ClientVersion,
            gitBranch = GitBranch,
        });

    /// <summary>A prompt that continues an existing conversation.</summary>
    public static string Prompt(string sessionId, string uuid, string parentUuid, string text, DateTimeOffset timestamp) =>
        Serialize(new
        {
            parentUuid,
            isSidechain = false,
            type = "user",
            message = new { role = UserRole, content = text },
            uuid,
            timestamp,
            userType = UserType,
            cwd = WorkingDirectory,
            sessionId,
            version = ClientVersion,
            gitBranch = GitBranch,
        });

    /// <summary>One assistant turn carrying a single block of prose.</summary>
    public static string AssistantText(
        string sessionId,
        string uuid,
        string parentUuid,
        string text,
        DateTimeOffset timestamp) =>
        Serialize(new
        {
            parentUuid,
            isSidechain = false,
            message = new
            {
                id = $"msg_{uuid}",
                role = AssistantRole,
                model = AssistantModel,
                content = new[] { new { type = TextBlockType, text } },
            },
            requestId = $"req_{uuid}",
            type = "assistant",
            uuid,
            timestamp,
            userType = UserType,
            cwd = WorkingDirectory,
            sessionId,
            version = ClientVersion,
            gitBranch = GitBranch,
        });

    /// <summary>
    /// A record of a kind nothing is indexed from, which still proves the session was being worked
    /// in at the moment it carries.
    /// </summary>
    public static string SystemNotice(string sessionId, string uuid, DateTimeOffset timestamp) =>
        Serialize(new
        {
            type = "system",
            content = "a tool finished",
            uuid,
            timestamp,
            cwd = WorkingDirectory,
            sessionId,
        });

    /// <summary>A title Claude generated, appended after the conversation it describes.</summary>
    public static string AiTitle(string sessionId, string title) =>
        Serialize(new { type = "ai-title", aiTitle = title, sessionId });

    /// <summary>A title the user chose, appended after the conversation it describes.</summary>
    public static string CustomTitle(string sessionId, string title) =>
        Serialize(new { type = "custom-title", customTitle = title, sessionId });

    private static string Serialize<TRecord>(TRecord record) => JsonSerializer.Serialize(record);
}
