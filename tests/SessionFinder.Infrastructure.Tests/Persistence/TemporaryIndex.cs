using Microsoft.Extensions.Logging.Abstractions;
﻿using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// A real index database in a throwaway directory, with the writer and reader wired the way a
/// head project wires them. Deliberately on disk rather than in memory: write-ahead logging and
/// external-content search tables behave differently there.
/// </summary>
internal sealed class TemporaryIndex : IDisposable
{
    private const string DatabaseFileName = "index.db";
    private const string RootFolderName = "session-finder-index";

    private readonly string _directory;
    private readonly SqliteIndexDatabase _database;

    public TemporaryIndex()
    {
        _directory = Path.Combine(Path.GetTempPath(), RootFolderName, Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);

        DatabasePath = Path.Combine(_directory, DatabaseFileName);
        _database = new SqliteIndexDatabase(Options.Create(new FinderOptions { IndexPath = DatabasePath }), NullLogger<SqliteIndexDatabase>.Instance);
        Writer = new SqliteSessionIndexWriter(_database);
        Reader = new SqliteSessionIndexReader(_database);
    }

    /// <summary>Absolute path of the database file.</summary>
    public string DatabasePath { get; }

    /// <summary>The writer under test.</summary>
    public SqliteSessionIndexWriter Writer { get; }

    /// <summary>The reader under test.</summary>
    public SqliteSessionIndexReader Reader { get; }

    /// <summary>
    /// Folds the write-ahead log into the database file so a query opened separately sees
    /// everything the writer committed.
    /// </summary>
    public Task CheckpointAsync() => _database.CheckpointAsync(CancellationToken.None);

    /// <summary>
    /// Opens a second connection for assertions that need to look at the stored rows directly.
    /// </summary>
    public SqliteConnection OpenInspector()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
        }.ToString());

        connection.Open();

        return connection;
    }

    /// <summary>
    /// Closes the writer and removes the directory. A file still held open must not fail the run:
    /// the operating system sweeps its own temp folder.
    /// </summary>
    public void Dispose()
    {
        Writer.Dispose();
        _database.Dispose();
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
