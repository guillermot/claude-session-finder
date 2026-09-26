using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Search;

namespace SessionFinder.Presentation.Tests.Search;

/// <summary>
/// Builds the shapes the view model consumes, so that a test says what it is about rather than
/// spelling out every required member of a ranked hit.
/// </summary>
internal static class SearchResults
{
    public static SearchSessionsResult Matching(params string[] titles) => new()
    {
        Hits = [.. titles.Select(title => Hit(title))],
        IsRecentFallback = false,
        CandidatesConsidered = titles.Length,
    };

    public static SearchSessionsResult Recent(params string[] titles) => new()
    {
        Hits = [.. titles.Select(title => Hit(title))],
        IsRecentFallback = true,
        CandidatesConsidered = titles.Length,
    };

    public static SessionHit Hit(
        string title,
        string folder = @"C:\git\Example",
        DateTimeOffset? lastActivity = null,
        string? branch = null,
        string? snippet = null) => new()
        {
            Session = new SessionSummary
            {
                SessionId = new SessionId(Guid.NewGuid()),
                FilePath = @"C:\transcripts\session.jsonl",
                Title = new SessionTitle(title, TitleSource.CustomTitle),
                Folder = WorkingFolder.FromTranscriptCwd(folder),
                LastActivity = lastActivity,
                GitBranch = branch,
                Snippet = snippet,
            },
            LexicalScore = 1.0,
            RecencyMultiplier = 1.0,
            Score = 1.0,
            MatchedKinds = [ChunkKind.UserPrompt],
            MatchedChunkCount = 1,
        };
}
