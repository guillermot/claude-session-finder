using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

/// <summary>
/// An activity reader over sessions held in memory, applying the window and the kind filter the
/// way the index does, and recording every window it was asked for.
/// </summary>
internal sealed class InMemoryActivityReader : ISessionActivityReader
{
    private readonly List<SessionActivity> _sessions = [];

    public List<(DateTimeOffset From, DateTimeOffset To, bool IncludeAssistantText)> Calls { get; } = [];

    public void Add(SessionActivity session) => _sessions.Add(session);

    public Task<IReadOnlyList<SessionActivity>> GetActivityAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        bool includeAssistantText,
        CancellationToken cancellationToken)
    {
        Calls.Add((from, to, includeAssistantText));

        IReadOnlyList<SessionActivity> found =
        [
            .. _sessions
                .Select(session => session with
                {
                    Chunks =
                    [
                        .. session.Chunks.Where(chunk =>
                            chunk.Timestamp >= from
                            && chunk.Timestamp < to
                            && (chunk.Kind == ChunkKind.UserPrompt
                                || (includeAssistantText && chunk.Kind == ChunkKind.AssistantText))),
                    ],
                })
                .Where(session => session.Chunks.Count > 0),
        ];

        return Task.FromResult(found);
    }
}
