using Microsoft.Data.Sqlite;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// A real on-disk SQLite database in a throwaway directory. The spike deliberately avoids
/// <c>:memory:</c>: write-ahead logging, file growth and FTS5 segment merging all behave
/// differently there, and those are the behaviours being measured.
/// </summary>
internal sealed class TemporaryDatabase : IDisposable
{
    private const string DatabaseFileName = "index.db";
    private const string RootFolderName = "session-finder-spike";

    private readonly string _directory;

    public TemporaryDatabase()
    {
        _directory = Path.Combine(Path.GetTempPath(), RootFolderName, Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);

        FilePath = Path.Combine(_directory, DatabaseFileName);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Pooling = false,
        }.ToString();
    }

    /// <summary>Absolute path of the database file.</summary>
    public string FilePath { get; }

    /// <summary>Connection string pointing at <see cref="FilePath"/> with pooling disabled.</summary>
    public string ConnectionString { get; }

    /// <summary>Size of the database file in bytes, zero when it has not been created yet.</summary>
    public long FileSizeBytes => File.Exists(FilePath) ? new FileInfo(FilePath).Length : 0;

    /// <summary>
    /// Opens a connection configured the way the real writer will be configured.
    /// </summary>
    public SqliteConnection OpenWriter()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        connection.Execute("PRAGMA journal_mode = WAL;");
        connection.Execute("PRAGMA synchronous = NORMAL;");
        connection.Execute("PRAGMA busy_timeout = 5000;");
        connection.Execute("PRAGMA temp_store = MEMORY;");

        return connection;
    }

    /// <summary>
    /// Releases the pooled handles and removes the directory. A file still held open must not
    /// fail the run: the operating system sweeps its own temp folder.
    /// </summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }
}
