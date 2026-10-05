using System.Globalization;
using Microsoft.Data.Sqlite;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// Reads the session index through short-lived read-only connections.
/// </summary>
/// <remarks>
/// Readers never share the writer's connection: opening and closing per query is cheap because the
/// provider pools by connection string, and it keeps a reader isolated from whatever transaction
/// the writer happens to be in.
/// </remarks>
public sealed class SqliteSessionIndexReader(SqliteIndexDatabase database) : ISessionIndexReader, ISessionActivityReader
{
    private const string SnippetOpenMarker = "[";
    private const string SnippetCloseMarker = "]";
    private const string SnippetEllipsis = "…";
    private const int SnippetTokenBudget = 12;
    private const char MatchedKindSeparator = ',';

    /// <inheritdoc />
    public async Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(database.DatabasePath))
        {
            return IndexStatistics.Empty(database.DatabasePath);
        }

        return await Task.Run(ReadStatistics, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionMatch>> SearchAsync(
        SessionSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(database.DatabasePath))
        {
            return [];
        }

        return await Task.Run(() => RunSearch(request, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionActivity>> GetActivityAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        bool includeAssistantText,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(database.DatabasePath) || from >= to)
        {
            return [];
        }

        return await Task
            .Run(() => ReadActivity(from, to, includeAssistantText, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private IReadOnlyList<SessionMatch> RunSearch(SessionSearchRequest request, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(database.ReadOnlyConnectionString);
        connection.Open();

        if (!IsCurrentSchema(connection))
        {
            return [];
        }

        var match = FtsQueryBuilder.Build(request.Text);

        using var command = match.IsEmpty
            ? CreateRecentSessionsCommand(connection, request)
            : CreateMatchCommand(connection, request, match);

        return ReadMatches(command, cancellationToken);
    }

    /// <summary>
    /// Builds the ranked lookup. The weighting happens in the statement because only the search
    /// index can say how relevant one chunk was, and the aggregation happens there too so a
    /// session that matched in fifty places crosses the boundary once rather than fifty times.
    /// </summary>
    private static SqliteCommand CreateMatchCommand(
        SqliteConnection connection,
        SessionSearchRequest request,
        FtsMatchQuery match)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql.SelectRankedMatches;

        command.Parameters.AddWithValue("$terms", match.TermsJson);
        command.Parameters.AddWithValue("$limit", request.CandidateLimit);
        command.Parameters.AddWithValue("$weight_title", request.Weights.Title);
        command.Parameters.AddWithValue("$weight_folder", request.Weights.Folder);
        command.Parameters.AddWithValue("$weight_last_prompt", request.Weights.LastPrompt);
        command.Parameters.AddWithValue("$weight_user_prompt", request.Weights.UserPrompt);
        command.Parameters.AddWithValue("$weight_assistant_text", request.Weights.AssistantText);
        command.Parameters.AddWithValue("$weight_unclassified", ChunkWeights.UnclassifiedWeight);
        command.Parameters.AddWithValue("$weight_repeat", request.RepeatMatchWeight);
        command.Parameters.AddWithValue("$weight_cooccurrence", request.CoOccurrenceWeight);
        command.Parameters.AddWithValue("$snippet_open", SnippetOpenMarker);
        command.Parameters.AddWithValue("$snippet_close", SnippetCloseMarker);
        command.Parameters.AddWithValue("$snippet_ellipsis", SnippetEllipsis);
        command.Parameters.AddWithValue("$snippet_tokens", SnippetTokenBudget);

        return command;
    }

    /// <summary>
    /// Reads every message in the window, grouped by session. The rows arrive ordered by session
    /// and then by time, so each session is assembled in one pass without a lookup.
    /// </summary>
    private IReadOnlyList<SessionActivity> ReadActivity(
        DateTimeOffset from,
        DateTimeOffset to,
        bool includeAssistantText,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(database.ReadOnlyConnectionString);
        connection.Open();

        if (!IsCurrentSchema(connection))
        {
            return [];
        }

        using var command = connection.CreateCommand();
        command.CommandText = Sql.SelectActivity;
        command.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$to", to.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$user_prompt", (int)ChunkKind.UserPrompt);
        command.Parameters.AddWithValue(
            "$second_kind",
            (int)(includeAssistantText ? ChunkKind.AssistantText : ChunkKind.UserPrompt));

        using var reader = command.ExecuteReader();
        var sessions = new List<SessionActivity>();
        SessionActivity? current = null;
        string? currentId = null;
        List<SearchChunk> chunks = [];

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sessionId = reader.GetString(ActivityColumn.SessionId);

            if (!string.Equals(currentId, sessionId, StringComparison.Ordinal))
            {
                AddSession(sessions, current, chunks);
                current = MapActivitySession(reader, sessionId);
                currentId = sessionId;
                chunks = [];
            }

            chunks.Add(new SearchChunk(
                (ChunkKind)reader.GetInt32(ActivityColumn.Kind),
                reader.GetString(ActivityColumn.Text),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(ActivityColumn.Timestamp))));
        }

        AddSession(sessions, current, chunks);

        return sessions;
    }

    private static void AddSession(List<SessionActivity> sessions, SessionActivity? session, List<SearchChunk> chunks)
    {
        if (session is not null)
        {
            sessions.Add(session with { Chunks = chunks });
        }
    }

    private static SessionActivity MapActivitySession(SqliteDataReader reader, string sessionId)
    {
        SessionId.TryParseFromFileName(sessionId, out var parsed);

        return new SessionActivity
        {
            SessionId = parsed,
            Title = new SessionTitle(
                reader.GetString(ActivityColumn.Title),
                (TitleSource)reader.GetInt32(ActivityColumn.TitleSource)),
            Folder = WorkingFolder.From(
                ReadNullableString(reader, ActivityColumn.FolderDisplay),
                (FolderSource)reader.GetInt32(ActivityColumn.FolderSource)),
            GitBranch = ReadNullableString(reader, ActivityColumn.GitBranch),
        };
    }

    private static SqliteCommand CreateRecentSessionsCommand(
        SqliteConnection connection,
        SessionSearchRequest request)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql.SelectRecentSessions;
        command.Parameters.AddWithValue("$limit", request.CandidateLimit);

        return command;
    }

    private static List<SessionMatch> ReadMatches(SqliteCommand command, CancellationToken cancellationToken)
    {
        using var reader = command.ExecuteReader();
        var matches = new List<SessionMatch>();

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            matches.Add(MapMatch(reader));
        }

        return matches;
    }

    private static SessionMatch MapMatch(SqliteDataReader reader) => new()
    {
        Session = MapSummary(reader),
        LexicalScore = reader.GetDouble(MatchColumn.Score),
        MatchedKinds = ParseMatchedKinds(ReadNullableString(reader, MatchColumn.MatchedKinds)),
        MatchedChunkCount = reader.GetInt32(MatchColumn.MatchCount),
    };

    private static SessionSummary MapSummary(SqliteDataReader reader)
    {
        SessionId.TryParseFromFileName(reader.GetString(MatchColumn.SessionId), out var sessionId);

        return new SessionSummary
        {
            SessionId = sessionId,
            FilePath = reader.GetString(MatchColumn.FilePath),
            Title = new SessionTitle(
                reader.GetString(MatchColumn.Title),
                (TitleSource)reader.GetInt32(MatchColumn.TitleSource)),
            Folder = WorkingFolder.From(
                ReadNullableString(reader, MatchColumn.FolderDisplay),
                (FolderSource)reader.GetInt32(MatchColumn.FolderSource)),
            GitBranch = ReadNullableString(reader, MatchColumn.GitBranch),
            LastActivity = reader.IsDBNull(MatchColumn.LastActivity)
                ? null
                : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(MatchColumn.LastActivity)),
            MessageCount = reader.GetInt32(MatchColumn.MessageCount),
            Snippet = ReadNullableString(reader, MatchColumn.Snippet),
        };
    }

    /// <summary>
    /// Turns the aggregated list of matched chunk kinds back into enum values. An unrecognised
    /// value is dropped rather than surfaced: which kinds matched is diagnostic information, and
    /// an index written by a newer build must not fail a search.
    /// </summary>
    private static IReadOnlyList<ChunkKind> ParseMatchedKinds(string? aggregated)
    {
        if (string.IsNullOrEmpty(aggregated))
        {
            return [];
        }

        return
        [
            .. aggregated
                .Split(MatchedKindSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseChunkKind)
                .Where(kind => kind.HasValue)
                .Select(kind => kind!.Value)
                .Order(),
        ];
    }

    private static ChunkKind? ParseChunkKind(string value)
    {
        if (!int.TryParse(value, CultureInfo.InvariantCulture, out var stored))
        {
            return null;
        }

        var kind = (ChunkKind)stored;

        return Enum.IsDefined(kind) ? kind : null;
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private IndexStatistics ReadStatistics()
    {
        using var connection = new SqliteConnection(database.ReadOnlyConnectionString);
        connection.Open();

        return IsCurrentSchema(connection)
            ? BuildStatistics(connection)
            : IndexStatistics.Empty(database.DatabasePath);
    }

    /// <summary>
    /// A database written by a different schema version is reported as absent rather than queried:
    /// the index is a rebuildable cache, so an unrecognised layout is a prompt to reindex, not an
    /// error to surface.
    /// </summary>
    private static bool IsCurrentSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.ReadUserVersion;

        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture)
            == SqliteSchema.CurrentVersion;
    }

    private IndexStatistics BuildStatistics(SqliteConnection connection)
    {
        var sessions = ReadSessionCounters(connection);
        var chunksByKind = ReadChunkCountsByKind(connection);

        return new IndexStatistics
        {
            DatabasePath = database.DatabasePath,
            DatabaseExists = true,
            DatabaseSizeBytes = database.FileSizeBytes,
            SessionCount = sessions.SessionCount,
            ChunkCount = chunksByKind.Values.Sum(),
            ChunksByKind = chunksByKind,
            SessionsWithParseErrors = sessions.ParseErrorCount,
            SessionsWithUnknownFolder = sessions.UnknownFolderCount,
            LastReconcileAt = ReadLastReconcile(connection),
            NewestActivity = sessions.NewestActivity,
        };
    }

    private static SessionCounters ReadSessionCounters(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.SelectSessionCounters;
        command.Parameters.AddWithValue("$unknown_folder", (int)FolderSource.Unknown);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return new SessionCounters(0, 0, 0, null);
        }

        return new SessionCounters(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)));
    }

    private static IReadOnlyDictionary<ChunkKind, int> ReadChunkCountsByKind(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.SelectChunkCountsByKind;

        using var reader = command.ExecuteReader();
        var counts = new Dictionary<ChunkKind, int>();

        while (reader.Read())
        {
            counts[(ChunkKind)reader.GetInt32(0)] = reader.GetInt32(1);
        }

        return counts;
    }

    private static DateTimeOffset? ReadLastReconcile(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.SelectMetaValue;
        command.Parameters.AddWithValue("$key", SqliteSchema.LastReconcileKey);

        var value = command.ExecuteScalar() as string;

        return long.TryParse(value, CultureInfo.InvariantCulture, out var unixMilliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
            : null;
    }

    /// <summary>
    /// Ordinals shared by <see cref="Sql.SelectRankedMatches"/> and
    /// <see cref="Sql.SelectRecentSessions"/>. Both statements project the same shape so one
    /// mapping serves a match and a plain recency listing.
    /// </summary>
    private static class MatchColumn
    {
        public const int SessionId = 0;
        public const int FilePath = 1;
        public const int Title = 2;
        public const int TitleSource = 3;
        public const int FolderDisplay = 4;
        public const int FolderSource = 5;
        public const int GitBranch = 6;
        public const int LastActivity = 7;
        public const int MessageCount = 8;
        public const int Snippet = 9;
        public const int Score = 10;
        public const int MatchedKinds = 11;
        public const int MatchCount = 12;
    }

    /// <summary>Ordinals of <see cref="Sql.SelectActivity"/>.</summary>
    private static class ActivityColumn
    {
        public const int SessionId = 0;
        public const int Title = 1;
        public const int TitleSource = 2;
        public const int FolderDisplay = 3;
        public const int FolderSource = 4;
        public const int GitBranch = 5;
        public const int Kind = 6;
        public const int Timestamp = 7;
        public const int Text = 8;
    }

    private readonly record struct SessionCounters(
        int SessionCount,
        int ParseErrorCount,
        int UnknownFolderCount,
        DateTimeOffset? NewestActivity);

    private static class Sql
    {
        public const string ReadUserVersion = "PRAGMA user_version;";

        public const string SelectSessionCounters =
            """
            SELECT count(*),
                   coalesce(sum(CASE WHEN parse_error IS NOT NULL THEN 1 ELSE 0 END), 0),
                   coalesce(sum(CASE WHEN folder_source = $unknown_folder THEN 1 ELSE 0 END), 0),
                   max(last_activity)
            FROM sessions;
            """;

        public const string SelectChunkCountsByKind =
            """
            SELECT kind, count(*) FROM chunks GROUP BY kind ORDER BY kind;
            """;

        /// <remarks>
        /// The session's own activity range is tested first so that the search can start from
        /// <c>ix_sessions_last_activity</c> and reach each session's chunks through
        /// <c>ix_chunks_session_id_kind</c>. Filtering the chunks by timestamp alone would read the
        /// whole table, because no index leads with the timestamp, and adding one would mean a new
        /// schema version and a rebuild for every user.
        /// </remarks>
        public const string SelectActivity =
            """
            SELECT s.session_id,
                   s.title,
                   s.title_source,
                   s.folder_display,
                   s.folder_source,
                   s.git_branch,
                   c.kind,
                   c.ts,
                   c.text
            FROM sessions s
            JOIN chunks c ON c.session_id = s.session_id
            WHERE s.last_activity >= $from
              AND (s.first_activity IS NULL OR s.first_activity < $to)
              AND c.kind IN ($user_prompt, $second_kind)
              AND c.ts >= $from
              AND c.ts < $to
            ORDER BY s.session_id, c.ts, c.id;
            """;

        public const string SelectMetaValue =
            """
            SELECT value FROM index_meta WHERE key = $key;
            """;

        /// <remarks>
        /// <para>
        /// Every typed term is matched on its own, in <c>hits</c>, which is what lets the rest of
        /// the statement ask two different questions of the same scan: whether a session mentions
        /// all of them, and how strongly each of its chunks matched.
        /// </para>
        /// <para>
        /// <c>qualified</c> is the conjunction, applied to the session and not to the chunk. A
        /// session survives only if it answered to every term somewhere in it. Requiring them in
        /// one chunk was measured against the real corpus and rejected: five of twelve plausible
        /// two-word questions returned nothing, because a conversation names its subject in one
        /// message and its problem in another.
        /// </para>
        /// <para>
        /// Adjacency is not lost by that, it is demoted from a filter to a ranking signal.
        /// <c>matches</c> folds the per-term hits back to one row per chunk, and a chunk that
        /// answered to several terms at once is multiplied up by
        /// <c>$weight_cooccurrence</c>. Measured over three independently derived query sets, this
        /// ranks the right session first more often than requiring one chunk ever did, while still
        /// finding everything the conjunction over the session finds.
        /// </para>
        /// <para>
        /// <c>bm25</c> returns a smaller number for a better match, so it is negated to make the
        /// score read the way everything downstream expects. A session then scores its strongest
        /// chunk plus a fraction of everything else it matched; that fraction is a setting because
        /// accumulating matches rewards length rather than relevance once the conjunction stops
        /// being per chunk.
        /// </para>
        /// <para>
        /// The snippet is taken from the term that matched the chunk best, using the rule that a
        /// query with exactly one <c>min</c> or <c>max</c> aggregate takes its bare columns from
        /// the row that produced it, and the session then shows the snippet of its strongest chunk.
        /// </para>
        /// <para>
        /// <c>MATERIALIZED</c> is load-bearing, not a hint about performance. Without it SQLite
        /// folds the inner select into the aggregate above it, the search functions end up outside
        /// the query that owns the <c>MATCH</c>, and the statement fails at run time with
        /// <c>unable to use function bm25 in the requested context</c>.
        /// </para>
        /// <para>
        /// The terms arrive as a JSON array so that one statement serves any number of them.
        /// Building an intersection with one branch per term was measured against this shape over
        /// the real index and was the same speed, which leaves no reason to assemble SQL by hand.
        /// </para>
        /// </remarks>
        public const string SelectRankedMatches =
            """
            WITH terms(term) AS (
                SELECT value FROM json_each($terms)
            ),
            hits AS MATERIALIZED (
                SELECT chunks_fts.rowid    AS chunk_id,
                       t.term              AS term,
                       (-bm25(chunks_fts)) AS relevance,
                       snippet(
                            chunks_fts, 0,
                            $snippet_open, $snippet_close, $snippet_ellipsis, $snippet_tokens
                       ) AS snippet
                FROM terms t
                JOIN chunks_fts ON chunks_fts MATCH t.term
            ),
            qualified AS (
                SELECT c.session_id AS session_id
                FROM hits h
                JOIN chunks c ON c.id = h.chunk_id
                GROUP BY c.session_id
                HAVING count(DISTINCT h.term) = (SELECT count(*) FROM terms)
            ),
            matches AS (
                SELECT c.session_id     AS session_id,
                       c.kind           AS kind,
                       max(h.relevance) AS top_relevance,
                       h.snippet        AS snippet,
                       sum(h.relevance)
                           * CASE c.kind
                                WHEN 1 THEN $weight_title
                                WHEN 2 THEN $weight_folder
                                WHEN 3 THEN $weight_last_prompt
                                WHEN 4 THEN $weight_user_prompt
                                WHEN 5 THEN $weight_assistant_text
                                ELSE $weight_unclassified
                             END
                           * (1 + $weight_cooccurrence * (count(DISTINCT h.term) - 1)) AS weighted
                FROM hits h
                JOIN chunks c ON c.id = h.chunk_id
                WHERE c.session_id IN (SELECT session_id FROM qualified)
                GROUP BY h.chunk_id, c.session_id, c.kind
            ),
            scored AS (
                SELECT session_id,
                       max(weighted) + $weight_repeat * (sum(weighted) - max(weighted)) AS score,
                       count(*)                  AS match_count,
                       group_concat(DISTINCT kind) AS matched_kinds
                FROM matches
                GROUP BY session_id
            ),
            best AS (
                SELECT session_id,
                       snippet,
                       row_number() OVER (PARTITION BY session_id ORDER BY weighted DESC) AS position
                FROM matches
            )
            SELECT s.session_id,
                   s.file_path,
                   s.title,
                   s.title_source,
                   s.folder_display,
                   s.folder_source,
                   s.git_branch,
                   s.last_activity,
                   s.message_count,
                   best.snippet,
                   scored.score,
                   scored.matched_kinds,
                   scored.match_count
            FROM scored
            JOIN best       ON best.session_id = scored.session_id AND best.position = 1
            JOIN sessions s ON s.session_id = scored.session_id
            ORDER BY scored.score DESC
            LIMIT $limit;
            """;

        /// <remarks>
        /// Answers an empty search box. Sessions that never recorded a timestamp sort last rather
        /// than first, which is what <c>NULL</c> would otherwise do under a descending order.
        /// </remarks>
        public const string SelectRecentSessions =
            """
            SELECT session_id,
                   file_path,
                   title,
                   title_source,
                   folder_display,
                   folder_source,
                   git_branch,
                   last_activity,
                   message_count,
                   NULL AS snippet,
                   0.0  AS score,
                   NULL AS matched_kinds,
                   0    AS match_count
            FROM sessions
            ORDER BY last_activity IS NULL, last_activity DESC
            LIMIT $limit;
            """;
    }
}
