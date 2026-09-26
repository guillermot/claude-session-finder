using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexSessionFile;
using SessionFinder.Core.Features.ReconcileIndex;
using SessionFinder.Infrastructure.FileSystem;
using SessionFinder.Infrastructure.Persistence;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Infrastructure.Tests.Indexing;

/// <summary>
/// A throwaway Claude configuration directory and a real index over it, wired the way a head
/// project wires them.
/// </summary>
/// <remarks>
/// <para>
/// Everything is a copy inside the temporary folder. The transcripts on the machine running these
/// tests are read by other tests and are never written to, renamed or removed by any of them.
/// </para>
/// <para>
/// The index is a file rather than an in-memory database on purpose: write-ahead logging and the
/// external-content search table behave differently in memory, and what is being measured here is
/// how the index behaves across several passes over the same file.
/// </para>
/// </remarks>
internal sealed class IndexingWorkspace : IDisposable
{
    private const string RootFolderName = "session-finder-indexing";
    private const string ConfigurationFolderName = "claude";
    private const string DatabaseFileName = "index.db";
    private const string TranscriptExtension = ".jsonl";

    private readonly string _root;
    private readonly SqliteIndexDatabase _database;

    /// <summary>
    /// Creates the temporary tree and opens an index over it.
    /// </summary>
    /// <param name="timeProvider">The clock the handlers stamp the index with.</param>
    public IndexingWorkspace(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _root = Path.Combine(Path.GetTempPath(), RootFolderName, Guid.NewGuid().ToString("n"));
        ProjectsDirectory = Path.Combine(_root, ConfigurationFolderName, ClaudeConfigurationDirectory.ProjectsFolderName);
        Directory.CreateDirectory(ProjectsDirectory);

        DatabasePath = Path.Combine(_root, DatabaseFileName);
        Options = Microsoft.Extensions.Options.Options.Create(new FinderOptions
        {
            ClaudeConfigDirectory = Path.Combine(_root, ConfigurationFolderName),
            IndexPath = DatabasePath,
        });

        _database = new SqliteIndexDatabase(Options, NullLogger<SqliteIndexDatabase>.Instance);
        Writer = new SqliteSessionIndexWriter(_database);
        Reader = new SqliteSessionIndexReader(_database);
        Catalog = new ClaudeProjectsCatalog(Options);

        Indexer = new IndexSessionFileHandler(
            new FileSystemTranscriptReader(),
            new JsonlSessionTranscriptParser(),
            Writer,
            timeProvider,
            NullLogger<IndexSessionFileHandler>.Instance);

        Reconciler = new ReconcileIndexHandler(
            Catalog,
            Indexer,
            Writer,
            timeProvider,
            NullLogger<ReconcileIndexHandler>.Instance);
    }

    /// <summary>The fake projects directory the catalogue and the watcher look at.</summary>
    public string ProjectsDirectory { get; }

    /// <summary>Absolute path of the index database.</summary>
    public string DatabasePath { get; }

    /// <summary>The settings a head would bind, pointing at this workspace.</summary>
    public IOptions<FinderOptions> Options { get; }

    /// <summary>The index writer under test.</summary>
    public SqliteSessionIndexWriter Writer { get; }

    /// <summary>The index reader under test.</summary>
    public SqliteSessionIndexReader Reader { get; }

    /// <summary>The catalogue that enumerates the fake projects tree.</summary>
    public ClaudeProjectsCatalog Catalog { get; }

    /// <summary>The handler that indexes one transcript.</summary>
    public IndexSessionFileHandler Indexer { get; }

    /// <summary>The handler that runs a whole pass.</summary>
    public ReconcileIndexHandler Reconciler { get; }

    /// <summary>
    /// Creates an empty transcript inside a project directory.
    /// </summary>
    /// <param name="projectFolderName">The lossy folder name Claude would have produced.</param>
    /// <param name="sessionId">The session identifier, which is also the file name.</param>
    /// <returns>A handle for mutating the file.</returns>
    public LiveTranscript CreateTranscript(string projectFolderName, string sessionId)
    {
        var projectDirectory = Path.Combine(ProjectsDirectory, projectFolderName);
        Directory.CreateDirectory(projectDirectory);

        var filePath = Path.Combine(projectDirectory, sessionId + TranscriptExtension);
        File.WriteAllBytes(filePath, []);

        return new LiveTranscript(filePath);
    }

    /// <summary>
    /// Indexes one transcript as it is on disk right now, going through the catalogue so the
    /// fingerprint is measured rather than assumed.
    /// </summary>
    /// <param name="transcript">The transcript to index.</param>
    /// <returns>What the pass did.</returns>
    public async Task<IndexSessionFileResult> IndexAsync(LiveTranscript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var file = await Catalog.DescribeAsync(transcript.FilePath, CancellationToken.None)
            ?? throw new InvalidOperationException($"The catalogue does not recognise {transcript.FilePath}.");

        return await Indexer.HandleAsync(new IndexSessionFileCommand { File = file }, CancellationToken.None);
    }

    /// <summary>
    /// Runs a whole pass over the fake projects tree.
    /// </summary>
    /// <returns>What the pass did.</returns>
    public Task<ReconcileIndexResult> ReconcileAsync() =>
        Reconciler.HandleAsync(ReconcileIndexCommand.Incremental, CancellationToken.None);

    /// <summary>
    /// Reads back what the index holds for a session.
    /// </summary>
    /// <param name="sessionId">The session to look up.</param>
    /// <returns>The stored state, or <see langword="null"/> when it was never indexed.</returns>
    public Task<IndexedSession?> GetIndexedSessionAsync(SessionId sessionId) =>
        Writer.GetIndexedSessionAsync(sessionId, CancellationToken.None);

    /// <summary>
    /// Opens a second connection for assertions that look at the stored rows directly.
    /// </summary>
    /// <returns>An open connection the caller must dispose.</returns>
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
    /// Closes the index and removes the temporary tree. A handle still open must not fail the run:
    /// the operating system sweeps its own temporary folder.
    /// </summary>
    public void Dispose()
    {
        Writer.Dispose();
        _database.Dispose();
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_root, recursive: true);
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
