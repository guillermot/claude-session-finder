using System.Globalization;
using Microsoft.Data.Sqlite;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// Writes sessions and their chunks to the SQLite index.
/// </summary>
/// <remarks>
/// <para>
/// One transaction per transcript, with the chunk inserts batched through a single prepared
/// statement. That is the difference between a pass over the corpus taking seconds and taking
/// minutes: every chunk insert fires the trigger that maintains the search index, and committing
/// per chunk would mean a disk sync per chunk.
/// </para>
/// <para>
/// The provider's asynchronous methods are synchronous underneath, so each operation is offloaded
/// once at its boundary and the cancellation token is honoured between rows. Nothing here
/// pretends an <c>await</c> buys overlapping input and output.
/// </para>
/// </remarks>
public sealed class SqliteSessionIndexWriter(SqliteIndexDatabase database) : ISessionIndexWriter, IDisposable
{
    private const int CancellationCheckInterval = 256;
    private const int MaxStoredTitleCandidateLength = 512;

    /// <summary>
    /// The chunk kinds a pass builds from the session rather than reading from the transcript, and
    /// therefore the only ones a resumed pass is allowed to replace.
    /// </summary>
    private static readonly ChunkKind[] DerivedChunkKinds = [ChunkKind.Title, ChunkKind.Folder, ChunkKind.LastPrompt];

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>Releases the write gate. The database connection is owned elsewhere.</summary>
    public void Dispose() => _writeGate.Dispose();

    /// <inheritdoc />
    public async Task<IndexedSession?> GetIndexedSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await RunExclusivelyAsync(
            () => ReadIndexedSession(connection, sessionId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteAsync(SessionIndexEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        await RunExclusivelyAsync(
            () => Write(connection, entry, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AppendAsync(SessionIndexEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        await RunExclusivelyAsync(
            () => Append(connection, entry, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ReviseTitleAsync(SessionTitleRevision revision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await RunExclusivelyAsync(
            () => ReviseTitle(connection, revision),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PruneMissingAsync(
        IReadOnlyCollection<SessionId> liveSessions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(liveSessions);

        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await RunExclusivelyAsync(
            () => PruneMissing(connection, liveSessions, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompactAsync(CancellationToken cancellationToken)
    {
        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        await RunExclusivelyAsync(
            () => Compact(connection),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task VacuumAsync(CancellationToken cancellationToken)
    {
        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        await RunExclusivelyAsync(
            () => Vacuum(connection),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordReconcileAsync(DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        var connection = await database.GetWriterConnectionAsync(cancellationToken).ConfigureAwait(false);

        await RunExclusivelyAsync(
            () => RecordReconcile(connection, completedAt),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serialises writes and moves the synchronous provider work off the calling thread, which is
    /// the only place an offload belongs when every call underneath is blocking anyway.
    /// </summary>
    private async Task<TResult> RunExclusivelyAsync<TResult>(Func<TResult> work, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task RunExclusivelyAsync(Action work, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static void Write(SqliteConnection connection, SessionIndexEntry entry, CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();

        UpsertSession(connection, transaction, entry);
        DeleteChunks(connection, transaction, entry.Document.SessionId);
        InsertChunks(connection, transaction, entry.Document, cancellationToken);

        transaction.Commit();
    }

    /// <summary>
    /// Merges the tail of a transcript into a session that is already indexed: the row absorbs what
    /// the tail knows, the derived chunks the pass produced are replaced, and everything written by
    /// earlier passes stays where it is.
    /// </summary>
    private static void Append(SqliteConnection connection, SessionIndexEntry entry, CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();

        MergeSession(connection, transaction, entry);
        DeleteDerivedChunks(connection, transaction, entry.Document);
        InsertChunks(connection, transaction, entry.Document, cancellationToken);

        transaction.Commit();
    }

    private static void MergeSession(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionIndexEntry entry)
    {
        var document = entry.Document;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.MergeSession;

        command.Parameters.AddWithValue("$session_id", document.SessionId.ToString());
        command.Parameters.AddWithValue("$file_path", entry.FilePath);
        command.Parameters.AddWithValue("$title", document.Title.Text);
        command.Parameters.AddWithValue("$title_source", (int)document.Title.Source);
        command.Parameters.AddWithValue("$custom_title", ToStoredCandidate(document.TitleCandidates.CustomTitle));
        command.Parameters.AddWithValue("$ai_title", ToStoredCandidate(document.TitleCandidates.AiTitle));
        command.Parameters.AddWithValue("$first_prompt", ToStoredCandidate(document.TitleCandidates.FirstPrompt));
        command.Parameters.AddWithValue("$folder_display", ToNullable(document.Folder.Display));
        command.Parameters.AddWithValue("$folder_key", ToNullable(document.Folder.Key));
        command.Parameters.AddWithValue("$folder_source", (int)document.Folder.Source);
        command.Parameters.AddWithValue("$git_branch", ToNullable(document.GitBranch));
        command.Parameters.AddWithValue("$last_prompt", ToNullable(document.LastPrompt));
        command.Parameters.AddWithValue("$first_activity", ToNullable(document.FirstActivity));
        command.Parameters.AddWithValue("$last_activity", ToNullable(document.LastActivity));
        command.Parameters.AddWithValue("$message_count", document.MessageCount);
        command.Parameters.AddWithValue("$file_size", entry.Fingerprint.Size);
        command.Parameters.AddWithValue("$mtime_ticks", entry.Fingerprint.MTimeTicks);
        command.Parameters.AddWithValue("$head_sha256", ToNullable(entry.Fingerprint.HeadSha256));
        command.Parameters.AddWithValue("$parse_offset", entry.Fingerprint.ParseOffset);
        command.Parameters.AddWithValue("$parse_error", ToNullable(document.ParseError));
        command.Parameters.AddWithValue("$indexed_at", entry.IndexedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$unknown_folder", (int)FolderSource.Unknown);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Removes the chunks the incoming pass rebuilt for itself, and only those: a pass that found
    /// no resume prompt must not delete the one the session already had.
    /// </summary>
    private static void DeleteDerivedChunks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionDocument document)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.DeleteSessionChunksOfKind;
        command.Parameters.AddWithValue("$session_id", document.SessionId.ToString());
        var kindParameter = command.Parameters.Add("$kind", SqliteType.Integer);

        foreach (var kind in DerivedChunkKinds)
        {
            if (!document.Chunks.Any(chunk => chunk.Kind == kind))
            {
                continue;
            }

            kindParameter.Value = (int)kind;
            command.ExecuteNonQuery();
        }
    }

    private static void UpsertSession(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionIndexEntry entry)
    {
        var document = entry.Document;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.UpsertSession;

        command.Parameters.AddWithValue("$session_id", document.SessionId.ToString());
        command.Parameters.AddWithValue("$file_path", entry.FilePath);
        command.Parameters.AddWithValue("$title", document.Title.Text);
        command.Parameters.AddWithValue("$title_source", (int)document.Title.Source);
        command.Parameters.AddWithValue("$custom_title", ToStoredCandidate(document.TitleCandidates.CustomTitle));
        command.Parameters.AddWithValue("$ai_title", ToStoredCandidate(document.TitleCandidates.AiTitle));
        command.Parameters.AddWithValue("$first_prompt", ToStoredCandidate(document.TitleCandidates.FirstPrompt));
        command.Parameters.AddWithValue("$folder_display", ToNullable(document.Folder.Display));
        command.Parameters.AddWithValue("$folder_key", ToNullable(document.Folder.Key));
        command.Parameters.AddWithValue("$folder_source", (int)document.Folder.Source);
        command.Parameters.AddWithValue("$git_branch", ToNullable(document.GitBranch));
        command.Parameters.AddWithValue("$last_prompt", ToNullable(document.LastPrompt));
        command.Parameters.AddWithValue("$first_activity", ToNullable(document.FirstActivity));
        command.Parameters.AddWithValue("$last_activity", ToNullable(document.LastActivity));
        command.Parameters.AddWithValue("$message_count", document.MessageCount);
        command.Parameters.AddWithValue("$file_size", entry.Fingerprint.Size);
        command.Parameters.AddWithValue("$mtime_ticks", entry.Fingerprint.MTimeTicks);
        command.Parameters.AddWithValue("$head_sha256", ToNullable(entry.Fingerprint.HeadSha256));
        command.Parameters.AddWithValue("$parse_offset", entry.Fingerprint.ParseOffset);
        command.Parameters.AddWithValue("$parse_error", ToNullable(document.ParseError));
        command.Parameters.AddWithValue("$indexed_at", entry.IndexedAt.ToUnixTimeMilliseconds());

        command.ExecuteNonQuery();
    }

    private static void DeleteChunks(SqliteConnection connection, SqliteTransaction transaction, SessionId sessionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.DeleteSessionChunks;
        command.Parameters.AddWithValue("$session_id", sessionId.ToString());

        command.ExecuteNonQuery();
    }

    private static void InsertChunks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionDocument document,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.InsertChunk;

        var sessionIdParameter = command.Parameters.Add("$session_id", SqliteType.Text);
        var kindParameter = command.Parameters.Add("$kind", SqliteType.Integer);
        var timestampParameter = command.Parameters.Add("$ts", SqliteType.Integer);
        var textParameter = command.Parameters.Add("$text", SqliteType.Text);

        sessionIdParameter.Value = document.SessionId.ToString();
        command.Prepare();

        var inserted = 0;

        foreach (var chunk in document.Chunks)
        {
            kindParameter.Value = (int)chunk.Kind;
            timestampParameter.Value = ToNullable(chunk.Timestamp);
            textParameter.Value = chunk.Text;

            command.ExecuteNonQuery();

            if (++inserted % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static bool ReviseTitle(SqliteConnection connection, SessionTitleRevision revision)
    {
        using var transaction = connection.BeginTransaction();

        var sessionId = revision.SessionId.ToString();

        if (!UpdateTitleRow(connection, transaction, revision, sessionId))
        {
            transaction.Rollback();
            return false;
        }

        DeleteTitleChunk(connection, transaction, sessionId);
        InsertTitleChunk(connection, transaction, revision, sessionId);

        transaction.Commit();

        return true;
    }

    private static bool UpdateTitleRow(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionTitleRevision revision,
        string sessionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.UpdateSessionTitle;

        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$title", revision.Title.Text);
        command.Parameters.AddWithValue("$title_source", (int)revision.Title.Source);
        command.Parameters.AddWithValue("$custom_title", ToStoredCandidate(revision.Candidates.CustomTitle));
        command.Parameters.AddWithValue("$ai_title", ToStoredCandidate(revision.Candidates.AiTitle));
        command.Parameters.AddWithValue("$first_prompt", ToStoredCandidate(revision.Candidates.FirstPrompt));
        command.Parameters.AddWithValue("$last_activity", ToNullable(revision.LastActivity));
        command.Parameters.AddWithValue("$file_size", revision.Fingerprint.Size);
        command.Parameters.AddWithValue("$mtime_ticks", revision.Fingerprint.MTimeTicks);
        command.Parameters.AddWithValue("$head_sha256", ToNullable(revision.Fingerprint.HeadSha256));
        command.Parameters.AddWithValue("$parse_offset", revision.Fingerprint.ParseOffset);
        command.Parameters.AddWithValue("$indexed_at", revision.IndexedAt.ToUnixTimeMilliseconds());

        return command.ExecuteNonQuery() > 0;
    }

    private static void DeleteTitleChunk(SqliteConnection connection, SqliteTransaction transaction, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.DeleteSessionChunksOfKind;
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$kind", (int)ChunkKind.Title);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Re-inserts the title chunk taking its timestamp from the session row, so a title replaced
    /// without re-reading the transcript still sorts with the rest of that session's chunks.
    /// </summary>
    private static void InsertTitleChunk(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionTitleRevision revision,
        string sessionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.InsertTitleChunkFromSession;
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$kind", (int)ChunkKind.Title);
        command.Parameters.AddWithValue("$text", revision.Title.Text);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes the sessions that are no longer on disk by staging the live identifiers in a
    /// temporary table: an identifier list long enough to cover the corpus does not belong in a
    /// statement, and staging keeps the delete a single indexed anti-join.
    /// </summary>
    private static int PruneMissing(
        SqliteConnection connection,
        IReadOnlyCollection<SessionId> liveSessions,
        CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();

        CreateLiveSessionStage(connection, transaction);
        FillLiveSessionStage(connection, transaction, liveSessions, cancellationToken);

        var pruned = DeleteSessionsOutsideStage(connection, transaction);

        transaction.Commit();

        return pruned;
    }

    private static void CreateLiveSessionStage(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.CreateLiveSessionStage;

        command.ExecuteNonQuery();
    }

    private static void FillLiveSessionStage(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyCollection<SessionId> liveSessions,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Sql.InsertLiveSession;

        var sessionIdParameter = command.Parameters.Add("$session_id", SqliteType.Text);
        command.Prepare();

        var staged = 0;

        foreach (var sessionId in liveSessions)
        {
            sessionIdParameter.Value = sessionId.ToString();
            command.ExecuteNonQuery();

            if (++staged % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static int DeleteSessionsOutsideStage(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var deleteChunks = connection.CreateCommand();
        deleteChunks.Transaction = transaction;
        deleteChunks.CommandText = Sql.DeleteChunksOfMissingSessions;
        deleteChunks.ExecuteNonQuery();

        using var deleteSessions = connection.CreateCommand();
        deleteSessions.Transaction = transaction;
        deleteSessions.CommandText = Sql.DeleteMissingSessions;

        return deleteSessions.ExecuteNonQuery();
    }

    /// <summary>
    /// Merges the search index segments so the pages left behind by replaced chunks are returned
    /// to the database and reused, instead of the file growing on every pass.
    /// </summary>
    private static void Compact(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.OptimizeSearchIndex;

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Rewrites the database file so that the space freed by replaced sessions is returned to the
    /// file system. It runs outside any transaction because the statement is not allowed inside
    /// one, and it holds the write gate because it rewrites everything.
    /// </summary>
    private static void Vacuum(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.ReclaimFreeSpace;

        command.ExecuteNonQuery();
    }

    private static void RecordReconcile(SqliteConnection connection, DateTimeOffset completedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.UpsertMeta;
        command.Parameters.AddWithValue("$key", SqliteSchema.LastReconcileKey);
        command.Parameters.AddWithValue(
            "$value",
            completedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

        command.ExecuteNonQuery();
    }

    private static IndexedSession? ReadIndexedSession(SqliteConnection connection, SessionId sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Sql.SelectSession;
        command.Parameters.AddWithValue("$session_id", sessionId.ToString());

        using var reader = command.ExecuteReader();

        return reader.Read() ? MapIndexedSession(reader, sessionId) : null;
    }

    private static IndexedSession MapIndexedSession(SqliteDataReader reader, SessionId sessionId)
    {
        var filePath = reader.GetString(Column.FilePath);

        var candidates = new SessionTitleCandidates
        {
            FileName = Path.GetFileName(filePath),
            CustomTitle = ReadNullableString(reader, Column.CustomTitle),
            AiTitle = ReadNullableString(reader, Column.AiTitle),
            FirstPrompt = ReadNullableString(reader, Column.FirstPrompt),
        };

        return new IndexedSession
        {
            SessionId = sessionId,
            FilePath = filePath,
            Fingerprint = new FileFingerprint(
                reader.GetInt64(Column.FileSize),
                reader.GetInt64(Column.MTimeTicks),
                ReadNullableString(reader, Column.HeadSha256),
                reader.GetInt64(Column.ParseOffset)),
            Title = new SessionTitle(reader.GetString(Column.Title), (TitleSource)reader.GetInt32(Column.TitleSource)),
            TitleCandidates = candidates,
            Folder = WorkingFolder.From(
                ReadNullableString(reader, Column.FolderDisplay),
                (FolderSource)reader.GetInt32(Column.FolderSource)),
            ParseError = ReadNullableString(reader, Column.ParseError),
        };
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    /// <summary>
    /// Caps a stored title candidate. Title resolution truncates far below this length, so the cap
    /// cannot change which title wins, and it keeps a pasted wall of text out of the session row.
    /// </summary>
    private static object ToStoredCandidate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return DBNull.Value;
        }

        return candidate.Length <= MaxStoredTitleCandidateLength
            ? candidate
            : candidate[..MaxStoredTitleCandidateLength];
    }

    private static object ToNullable(string? value) =>
        string.IsNullOrEmpty(value) ? DBNull.Value : value;

    private static object ToNullable(DateTimeOffset? value) =>
        value is { } present ? present.ToUnixTimeMilliseconds() : DBNull.Value;

    /// <summary>
    /// Ordinals of <see cref="Sql.SelectSession"/>, named so the mapping does not depend on
    /// counting commas in the statement above it.
    /// </summary>
    private static class Column
    {
        public const int FilePath = 0;
        public const int Title = 1;
        public const int TitleSource = 2;
        public const int CustomTitle = 3;
        public const int AiTitle = 4;
        public const int FirstPrompt = 5;
        public const int FolderDisplay = 6;
        public const int FolderSource = 7;
        public const int FileSize = 8;
        public const int MTimeTicks = 9;
        public const int HeadSha256 = 10;
        public const int ParseOffset = 11;
        public const int ParseError = 12;
    }

    private static class Sql
    {
        public const string UpsertSession =
            """
            INSERT INTO sessions (
                session_id, file_path, title, title_source, custom_title, ai_title, first_prompt,
                folder_display, folder_key, folder_source, git_branch, last_prompt,
                first_activity, last_activity, message_count,
                file_size, mtime_ticks, head_sha256, parse_offset, parse_error, indexed_at
            )
            VALUES (
                $session_id, $file_path, $title, $title_source, $custom_title, $ai_title, $first_prompt,
                $folder_display, $folder_key, $folder_source, $git_branch, $last_prompt,
                $first_activity, $last_activity, $message_count,
                $file_size, $mtime_ticks, $head_sha256, $parse_offset, $parse_error, $indexed_at
            )
            ON CONFLICT(session_id) DO UPDATE SET
                file_path      = excluded.file_path,
                title          = excluded.title,
                title_source   = excluded.title_source,
                custom_title   = excluded.custom_title,
                ai_title       = excluded.ai_title,
                first_prompt   = excluded.first_prompt,
                folder_display = excluded.folder_display,
                folder_key     = excluded.folder_key,
                folder_source  = excluded.folder_source,
                git_branch     = excluded.git_branch,
                last_prompt    = excluded.last_prompt,
                first_activity = excluded.first_activity,
                last_activity  = excluded.last_activity,
                message_count  = excluded.message_count,
                file_size      = excluded.file_size,
                mtime_ticks    = excluded.mtime_ticks,
                head_sha256    = excluded.head_sha256,
                parse_offset   = excluded.parse_offset,
                parse_error    = excluded.parse_error,
                indexed_at     = excluded.indexed_at;
            """;

        /// <remarks>
        /// <para>
        /// Everything a pass over the tail knows in full is assigned; everything it can only know
        /// partially is merged. The message count accumulates, the first activity can only move
        /// earlier and the last can only move later, and the branch and the resume prompt survive a
        /// tail that did not mention them.
        /// </para>
        /// <para>
        /// The folder is the one field that resists a tail: the record carrying the authoritative
        /// working directory sits at the top of the file, far behind the offset a resumed pass
        /// starts at. It is therefore only written when it would replace not knowing with knowing.
        /// </para>
        /// <para>
        /// <c>min</c> and <c>max</c> return null as soon as one argument is null, which is why each
        /// is wrapped in a <c>coalesce</c> that falls back to whichever side actually has a value.
        /// </para>
        /// </remarks>
        public const string MergeSession =
            """
            UPDATE sessions
            SET file_path      = $file_path,
                title          = $title,
                title_source   = $title_source,
                custom_title   = $custom_title,
                ai_title       = $ai_title,
                first_prompt   = $first_prompt,
                folder_display = CASE WHEN $folder_source = $unknown_folder THEN folder_display ELSE $folder_display END,
                folder_key     = CASE WHEN $folder_source = $unknown_folder THEN folder_key     ELSE $folder_key     END,
                folder_source  = CASE WHEN $folder_source = $unknown_folder THEN folder_source  ELSE $folder_source  END,
                git_branch     = coalesce($git_branch, git_branch),
                last_prompt    = coalesce($last_prompt, last_prompt),
                first_activity = coalesce(min(first_activity, $first_activity), first_activity, $first_activity),
                last_activity  = coalesce(max(last_activity, $last_activity), last_activity, $last_activity),
                message_count  = message_count + $message_count,
                file_size      = $file_size,
                mtime_ticks    = $mtime_ticks,
                head_sha256    = coalesce($head_sha256, head_sha256),
                parse_offset   = $parse_offset,
                parse_error    = $parse_error,
                indexed_at     = $indexed_at
            WHERE session_id = $session_id;
            """;

        /// <remarks>
        /// The last activity is merged rather than assigned, by the same rule the tail merge uses:
        /// a pass that read a tail carrying no timestamp must leave the stored moment alone, and a
        /// pass that read a later one must not be the reason the session looks stale.
        /// </remarks>
        public const string UpdateSessionTitle =
            """
            UPDATE sessions
            SET title         = $title,
                title_source  = $title_source,
                custom_title  = $custom_title,
                ai_title      = $ai_title,
                first_prompt  = $first_prompt,
                last_activity = coalesce(max(last_activity, $last_activity), last_activity, $last_activity),
                file_size     = $file_size,
                mtime_ticks   = $mtime_ticks,
                head_sha256   = $head_sha256,
                parse_offset  = $parse_offset,
                indexed_at    = $indexed_at
            WHERE session_id = $session_id;
            """;

        public const string SelectSession =
            """
            SELECT file_path, title, title_source, custom_title, ai_title, first_prompt,
                   folder_display, folder_source,
                   file_size, mtime_ticks, head_sha256, parse_offset, parse_error
            FROM sessions
            WHERE session_id = $session_id;
            """;

        public const string DeleteSessionChunks =
            """
            DELETE FROM chunks WHERE session_id = $session_id;
            """;

        public const string DeleteSessionChunksOfKind =
            """
            DELETE FROM chunks WHERE session_id = $session_id AND kind = $kind;
            """;

        public const string InsertChunk =
            """
            INSERT INTO chunks (session_id, kind, ts, text) VALUES ($session_id, $kind, $ts, $text);
            """;

        public const string InsertTitleChunkFromSession =
            """
            INSERT INTO chunks (session_id, kind, ts, text)
            SELECT session_id, $kind, last_activity, $text
            FROM sessions
            WHERE session_id = $session_id;
            """;

        public const string CreateLiveSessionStage =
            """
            CREATE TEMP TABLE IF NOT EXISTS live_sessions (session_id TEXT NOT NULL PRIMARY KEY);
            DELETE FROM live_sessions;
            """;

        public const string InsertLiveSession =
            """
            INSERT OR IGNORE INTO live_sessions (session_id) VALUES ($session_id);
            """;

        public const string DeleteChunksOfMissingSessions =
            """
            DELETE FROM chunks
            WHERE session_id NOT IN (SELECT session_id FROM live_sessions);
            """;

        public const string DeleteMissingSessions =
            """
            DELETE FROM sessions
            WHERE session_id NOT IN (SELECT session_id FROM live_sessions);
            """;

        public const string OptimizeSearchIndex =
            """
            INSERT INTO chunks_fts(chunks_fts) VALUES ('optimize');
            """;

        /// <remarks>
        /// The checkpoint is part of the operation, not a flourish. Under write-ahead logging the
        /// rewrite lands in the log, so the database file keeps its old size until the log is
        /// folded back in — measured on a full index, the file did not shrink by a single byte
        /// without this.
        /// </remarks>
        public const string ReclaimFreeSpace =
            """
            VACUUM;
            PRAGMA wal_checkpoint(TRUNCATE);
            """;

        public const string UpsertMeta =
            """
            INSERT INTO index_meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
    }
}
