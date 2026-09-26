using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// Owns the index schema and its <c>PRAGMA user_version</c> migration policy.
/// </summary>
/// <remarks>
/// <para>
/// Layout: a plain <c>chunks</c> table holds the searchable text, and <c>chunks_fts</c> is an
/// external-content FTS5 table kept in step by triggers. The alternative, a contentless table
/// with <c>contentless_delete=1</c>, is available in the bundled SQLite and measured slightly
/// faster and smaller, but it returns <c>NULL</c> for every indexed column, which would make the
/// index write-only: no snippets, no <c>rebuild</c>, and no way to inspect what the transcript
/// parser actually produced without re-reading every transcript.
/// </para>
/// <para>
/// A third layout, a content-owning FTS5 table filtered by an <c>UNINDEXED</c> session column,
/// is rejected outright: removing one session's chunks scans the whole table, and the measured
/// cost grew with the corpus.
/// </para>
/// <para>
/// The index is a derived cache, never a system of record. Any version other than the current
/// one is dropped and rebuilt rather than migrated, which removes a whole class of work.
/// </para>
/// </remarks>
public static class SqliteSchema
{
    /// <summary>Schema version written to <c>PRAGMA user_version</c>.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Key under which <c>index_meta</c> holds the last reconcile timestamp.</summary>
    public const string LastReconcileKey = "last_reconcile_at";

    private const int EmptyDatabaseVersion = 0;

    /// <summary>
    /// Brings the database to <see cref="CurrentVersion"/>, dropping and recreating everything
    /// when it holds any other version.
    /// </summary>
    /// <param name="connection">An open writer connection.</param>
    /// <param name="cancellationToken">Cancels before each statement batch.</param>
    /// <returns><see langword="true"/> when the schema was created or recreated.</returns>
    public static async Task<bool> EnsureCurrentAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var existingVersion = await ReadUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);

        if (existingVersion == CurrentVersion)
        {
            return false;
        }

        if (existingVersion != EmptyDatabaseVersion)
        {
            await ExecuteAsync(connection, Sql.DropEverything, cancellationToken).ConfigureAwait(false);
        }

        await ExecuteAsync(connection, Sql.CreateSessions, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, Sql.CreateIndexMeta, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, Sql.CreateChunks, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, Sql.CreateChunksFts, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, Sql.CreateChunksSyncTriggers, cancellationToken).ConfigureAwait(false);
        await WriteUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Reads <c>PRAGMA user_version</c>. A database that has never been written reports zero.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored schema version.</returns>
    public static async Task<int> ReadUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = Sql.ReadUserVersion;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// <c>PRAGMA</c> statements do not accept bound parameters, so the version is formatted into
    /// the statement. <see cref="CurrentVersion"/> is a compile-time constant, so there is no
    /// caller-controlled input here.
    /// </remarks>
    private static async Task WriteUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = string.Create(CultureInfo.InvariantCulture, $"PRAGMA user_version = {CurrentVersion};");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static class Sql
    {
        public const string ReadUserVersion = "PRAGMA user_version;";

        public const string DropEverything =
            """
            DROP TRIGGER IF EXISTS chunks_after_update;
            DROP TRIGGER IF EXISTS chunks_after_delete;
            DROP TRIGGER IF EXISTS chunks_after_insert;
            DROP TABLE   IF EXISTS chunks_fts;
            DROP TABLE   IF EXISTS chunks;
            DROP TABLE   IF EXISTS index_meta;
            DROP TABLE   IF EXISTS sessions;
            """;

        /// <remarks>
        /// The three candidate columns exist so a later pass can resolve the title again instead
        /// of re-reading the transcript: the resolved <c>title</c> alone cannot say what the other
        /// candidates were, and a pass over the tail of a file can never see the first prompt.
        /// </remarks>
        public const string CreateSessions =
            """
            CREATE TABLE sessions (
                session_id     TEXT    NOT NULL PRIMARY KEY,
                file_path      TEXT    NOT NULL UNIQUE,
                title          TEXT    NOT NULL,
                title_source   INTEGER NOT NULL,
                custom_title   TEXT    NULL,
                ai_title       TEXT    NULL,
                first_prompt   TEXT    NULL,
                folder_display TEXT    NULL,
                folder_key     TEXT    NULL,
                folder_source  INTEGER NOT NULL,
                git_branch     TEXT    NULL,
                last_prompt    TEXT    NULL,
                first_activity INTEGER NULL,
                last_activity  INTEGER NULL,
                message_count  INTEGER NOT NULL DEFAULT 0,
                file_size      INTEGER NOT NULL DEFAULT 0,
                mtime_ticks    INTEGER NOT NULL DEFAULT 0,
                head_sha256    TEXT    NULL,
                parse_offset   INTEGER NOT NULL DEFAULT 0,
                parse_error    TEXT    NULL,
                indexed_at     INTEGER NOT NULL
            );

            CREATE INDEX ix_sessions_last_activity ON sessions(last_activity DESC);
            CREATE INDEX ix_sessions_folder_key    ON sessions(folder_key);
            """;

        public const string CreateIndexMeta =
            """
            CREATE TABLE index_meta (
                key   TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;

        public const string CreateChunks =
            """
            CREATE TABLE chunks (
                id         INTEGER PRIMARY KEY,
                session_id TEXT    NOT NULL,
                kind       INTEGER NOT NULL,
                ts         INTEGER NULL,
                text       TEXT    NOT NULL
            );

            CREATE INDEX ix_chunks_session_id_kind ON chunks(session_id, kind);
            """;

        public const string CreateChunksFts =
            """
            CREATE VIRTUAL TABLE chunks_fts USING fts5(
                text,
                content='chunks',
                content_rowid='id',
                tokenize='unicode61 remove_diacritics 2',
                prefix='2 3 4'
            );
            """;

        public const string CreateChunksSyncTriggers =
            """
            CREATE TRIGGER chunks_after_insert AFTER INSERT ON chunks BEGIN
                INSERT INTO chunks_fts(rowid, text) VALUES (new.id, new.text);
            END;

            CREATE TRIGGER chunks_after_delete AFTER DELETE ON chunks BEGIN
                INSERT INTO chunks_fts(chunks_fts, rowid, text) VALUES ('delete', old.id, old.text);
            END;

            CREATE TRIGGER chunks_after_update AFTER UPDATE ON chunks BEGIN
                INSERT INTO chunks_fts(chunks_fts, rowid, text) VALUES ('delete', old.id, old.text);
                INSERT INTO chunks_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """;
    }
}
