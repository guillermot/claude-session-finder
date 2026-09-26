using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// Owns the index file: where it lives, the one long-lived writer connection, and the read-only
/// connection string everyone else opens.
/// </summary>
/// <remarks>
/// <para>
/// SQLite permits exactly one writer, so the writer connection is created once and handed out;
/// write-ahead logging is what lets readers work at the same time. Readers open and close per
/// query against a read-only connection string, which the provider pools for them.
/// </para>
/// <para>
/// The database is a derived cache. Nothing here tries to preserve it: a file holding any other
/// schema version is dropped and rebuilt on first use.
/// </para>
/// </remarks>
public sealed class SqliteIndexDatabase : IDisposable, IAsyncDisposable
{
    private const string CheckpointStatement = "PRAGMA wal_checkpoint(TRUNCATE);";
    private const string WriteAheadLogSuffix = "-wal";
    private const string SharedMemorySuffix = "-shm";
    private const int FileIsNotADatabase = 26;
    private const int DatabaseDiskImageIsMalformed = 11;

    private const string WriterPragmas =
        """
        PRAGMA journal_mode = WAL;
        PRAGMA synchronous  = NORMAL;
        PRAGMA busy_timeout = 5000;
        PRAGMA temp_store   = MEMORY;
        """;

    private readonly SemaphoreSlim _writerGate = new(1, 1);

    private SqliteConnection? _writerConnection;
    private bool _disposed;

    private readonly ILogger<SqliteIndexDatabase> _logger;

    /// <summary>
    /// Resolves the database location from configuration.
    /// </summary>
    /// <param name="options">The bound settings; a blank index path selects the default location.</param>
    /// <param name="logger">Where an index that had to be thrown away is recorded.</param>
    public SqliteIndexDatabase(IOptions<FinderOptions> options, ILogger<SqliteIndexDatabase> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        DatabasePath = ApplicationPaths.ResolveIndexPath(options.Value.IndexPath);
        ReadOnlyConnectionString = BuildReadOnlyConnectionString(DatabasePath);
    }

    /// <summary>Absolute path of the index database, whether or not it exists yet.</summary>
    public string DatabasePath { get; }

    /// <summary>Connection string for readers. Opening it does not create the file.</summary>
    public string ReadOnlyConnectionString { get; }

    /// <summary>
    /// Size of the index on disk in bytes, zero when it has not been created yet. Excludes the
    /// write-ahead log, which is folded back into the file by <see cref="CheckpointAsync"/>.
    /// </summary>
    public long FileSizeBytes => File.Exists(DatabasePath) ? new FileInfo(DatabasePath).Length : 0;

    /// <summary>
    /// Returns the single writer connection, creating the file, the pragmas and the schema on
    /// first use.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open writer connection, owned by this instance.</returns>
    public async ValueTask<SqliteConnection> GetWriterConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_writerConnection is { } existing)
        {
            return existing;
        }

        await _writerGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _writerConnection ??= await OpenWriterAsync(cancellationToken).ConfigureAwait(false);

            return _writerConnection;
        }
        finally
        {
            _writerGate.Release();
        }
    }

    /// <summary>
    /// Whether an exception from the provider says the file on disk is not a usable database.
    /// </summary>
    /// <remarks>
    /// Only these two codes mean the file itself is beyond use. A locked, busy or read-only
    /// database is a passing condition and must never be answered by deleting the index.
    /// </remarks>
    /// <param name="exception">What the provider threw.</param>
    /// <returns><see langword="true"/> when the file is corrupt or is not a database at all.</returns>
    public static bool IsCorruption(SqliteException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.SqliteErrorCode is FileIsNotADatabase or DatabaseDiskImageIsMalformed;
    }

    /// <summary>
    /// Throws away the index and opens an empty one in its place.
    /// </summary>
    /// <remarks>
    /// The index is a derived cache of files that are still on disk, so the cost of this is the
    /// time of one pass over the transcripts and the benefit is an application that survives a
    /// corrupt file instead of failing every search until someone deletes it by hand.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the reopen.</param>
    /// <returns>A task that completes once an empty index is open.</returns>
    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writerGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            CloseWriter();
            DeleteDatabaseFiles();

            _writerConnection = await OpenWriterAsync(cancellationToken).ConfigureAwait(false);

            IndexDatabaseLog.IndexRecreated(_logger, DatabasePath);
        }
        finally
        {
            _writerGate.Release();
        }
    }

    /// <summary>
    /// Folds the write-ahead log back into the database file, so the size reported afterwards is
    /// the real size of the index rather than the size of a partially checkpointed one.
    /// </summary>
    /// <param name="cancellationToken">Cancels the checkpoint.</param>
    /// <returns>A task that completes once the log is folded in.</returns>
    public async Task CheckpointAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _writerConnection is not { } connection)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = CheckpointStatement;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checkpoints and closes the writer connection.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Checkpoint();

        _disposed = true;
        _writerConnection?.Dispose();
        _writerConnection = null;
        _writerGate.Dispose();
    }

    /// <summary>Checkpoints and closes the writer connection.</summary>
    /// <remarks>
    /// The provider has no genuinely asynchronous close, so this is <see cref="Dispose"/>. It
    /// exists because a container that disposes asynchronously looks for it.
    /// </remarks>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Folds the log back in on the way out, and gives up quietly when the file will not have it.
    /// A checkpoint that fails during disposal must not become the exception that ends the process,
    /// because the file it was protecting is a cache that is about to be rebuilt anyway.
    /// </summary>
    private void Checkpoint()
    {
        if (_writerConnection is not { } connection)
        {
            return;
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = CheckpointStatement;
            command.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            return;
        }
    }

    private static string BuildReadOnlyConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

    /// <summary>
    /// Builds the writer's connection string. Pooling is off: there is exactly one writer for the
    /// life of the process, so the pool would never hand out a second connection, and a pooled
    /// connection keeps the file open after it is disposed — which is the difference between an
    /// index that can be thrown away and rebuilt and one that cannot be deleted.
    /// </summary>
    private static string BuildWriterConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

    /// <summary>
    /// Opens the writer, and answers a file that turns out not to be a usable database by deleting
    /// it and opening an empty one. The second attempt is against a file that has just been
    /// removed, so it cannot fail for the same reason twice.
    /// </summary>
    private async Task<SqliteConnection> OpenWriterAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await OpenWriterOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (IsCorruption(exception))
        {
            IndexDatabaseLog.IndexUnusable(_logger, exception, DatabasePath);

            DeleteDatabaseFiles();

            var connection = await OpenWriterOnceAsync(cancellationToken).ConfigureAwait(false);

            IndexDatabaseLog.IndexRecreated(_logger, DatabasePath);

            return connection;
        }
    }

    private async Task<SqliteConnection> OpenWriterOnceAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        var connection = new SqliteConnection(BuildWriterConnectionString(DatabasePath));

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ApplyWriterPragmasAsync(connection, cancellationToken).ConfigureAwait(false);
            await SqliteSchema.EnsureCurrentAsync(connection, cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void CloseWriter()
    {
        _writerConnection?.Dispose();
        _writerConnection = null;

        SqliteConnection.ClearAllPools();
    }

    /// <summary>
    /// Removes the index and the two files the write-ahead log keeps beside it. Leaving those
    /// behind would have the next open read a log belonging to a database that no longer exists.
    /// </summary>
    /// <remarks>
    /// The pools are emptied first. A connection that has been disposed is returned to the
    /// provider's pool rather than closed, and a pooled connection still holds the file open, so
    /// deleting without this fails with a sharing violation on exactly the file that has to go.
    /// </remarks>
    private void DeleteDatabaseFiles()
    {
        SqliteConnection.ClearAllPools();

        File.Delete(DatabasePath);
        File.Delete(DatabasePath + WriteAheadLogSuffix);
        File.Delete(DatabasePath + SharedMemorySuffix);
    }

    private static async Task ApplyWriterPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = WriterPragmas;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
