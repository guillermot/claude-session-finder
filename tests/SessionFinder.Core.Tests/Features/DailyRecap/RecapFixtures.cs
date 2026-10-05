using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

/// <summary>
/// Builds sessions and instants for recap tests in one fixed time zone three hours behind UTC, with
/// no daylight saving, so a test reads in the local times it is about.
/// </summary>
internal static class RecapFixtures
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("Recap/Test", Offset, "Recap test zone", "Recap test zone");

    /// <summary>Monday, 5 October 2026, at noon local time.</summary>
    public static readonly DateTimeOffset Now = At(10, 5, 12);

    public static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, Offset);

    public static DateOnly Day(int month, int day) => new(2026, month, day);

    public static SearchChunk Prompt(DateTimeOffset at, string text) => new(ChunkKind.UserPrompt, text, at);

    public static SearchChunk Reply(DateTimeOffset at, string text) => new(ChunkKind.AssistantText, text, at);

    public static SessionActivity Session(
        string title,
        string folder,
        params SearchChunk[] chunks) => Session(title, folder, branch: "main", chunks);

    public static SessionActivity Session(
        string title,
        string folder,
        string? branch,
        params SearchChunk[] chunks) => new()
    {
        SessionId = new SessionId(Guid.NewGuid()),
        Title = new SessionTitle(title, TitleSource.AiTitle),
        Folder = WorkingFolder.FromTranscriptCwd(folder),
        GitBranch = branch,
        Chunks = chunks,
    };
}
