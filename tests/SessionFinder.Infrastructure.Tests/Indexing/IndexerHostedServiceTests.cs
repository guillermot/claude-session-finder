using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionFinder.Core;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Tests.Indexing;

/// <summary>
/// The background indexer as a head runs it: started, left to follow a transcript that is being
/// written, and then stopped.
/// </summary>
/// <remarks>
/// The clock here is the real one, because what is being exercised is the part that cannot be
/// simulated — the operating system reporting a file change, and the host ending a background
/// service. The buffer's two readiness edges are proven against a controlled clock elsewhere; the
/// windows configured here are small so the same behaviour is observable in a second rather than
/// in twenty.
/// </remarks>
public sealed class IndexerHostedServiceTests : IDisposable
{
    private const string RootFolderName = "session-finder-hosted-indexer";
    private const string ConfigurationFolderName = "claude";
    private const string DatabaseFileName = "index.db";
    private const string WriteAheadLogSuffix = "-wal";
    private const string ProjectFolder = "C--work-project";
    private const string SessionIdentifier = "7e8f9a0b-1c2d-4e3f-8a4b-5c6d7e8f9a0b";
    private const string OpeningPromptText = "reconcile the kiwi deposits";
    private const string AppendedPromptText = "now check the papaya transfers";

    private const int PollMilliseconds = 50;
    private const int SettleMilliseconds = 200;
    private const int MaximumWaitMilliseconds = 1_000;

    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IndexingDeadline = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BetweenChecks = TimeSpan.FromMilliseconds(50);
    private static readonly DateTimeOffset Activity = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _configurationDirectory;
    private readonly string _databasePath;
    private readonly LiveTranscript _transcript;

    public IndexerHostedServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), RootFolderName, Guid.NewGuid().ToString("n"));
        _configurationDirectory = Path.Combine(_root, ConfigurationFolderName);
        _databasePath = Path.Combine(_root, DatabaseFileName);

        var projectDirectory = Path.Combine(
            _configurationDirectory,
            ClaudeConfigurationDirectory.ProjectsFolderName,
            ProjectFolder);
        Directory.CreateDirectory(projectDirectory);

        _transcript = new LiveTranscript(Path.Combine(projectDirectory, SessionIdentifier + ".jsonl"));
        _transcript.Append(TranscriptLine.RootPrompt(SessionIdentifier, "u1", OpeningPromptText, Activity));
    }

    [Fact]
    public async Task ExecuteAsync_TranscriptAlreadyOnDisk_IndexesItOnTheFirstPass()
    {
        using var host = BuildHost();
        var writer = host.Services.GetRequiredService<ISessionIndexWriter>();

        await host.StartAsync();
        var indexed = await WaitForAsync(writer, stored => stored.Fingerprint.ParseOffset > 0);
        await host.StopAsync();

        indexed.Fingerprint.ParseOffset.Should().Be(_transcript.SizeBytes);
    }

    [Fact]
    public async Task ExecuteAsync_TranscriptAppendedToWhileRunning_IndexesTheAppendedBytes()
    {
        using var host = BuildHost();
        var writer = host.Services.GetRequiredService<ISessionIndexWriter>();

        await host.StartAsync();
        var afterFirstPass = await WaitForAsync(writer, stored => stored.Fingerprint.ParseOffset > 0);
        _transcript.Append(TranscriptLine.Prompt(SessionIdentifier, "u2", "u1", AppendedPromptText, Activity));

        var afterTheAppend = await WaitForAsync(
            writer,
            stored => stored.Fingerprint.ParseOffset > afterFirstPass.Fingerprint.ParseOffset);
        await host.StopAsync();

        afterTheAppend.Fingerprint.ParseOffset.Should().Be(_transcript.SizeBytes);
    }

    [Fact]
    public async Task StopAsync_WhileFollowingChanges_FoldsTheWriteAheadLogBackIntoTheIndex()
    {
        using var host = BuildHost();
        var writer = host.Services.GetRequiredService<ISessionIndexWriter>();

        await host.StartAsync();
        await WaitForAsync(writer, stored => stored.Fingerprint.ParseOffset > 0);
        await host.StopAsync();

        WriteAheadLogLength().Should().Be(
            0,
            "the checkpoint is the last thing the indexer does, so a folded log is what says the stop ran to the end rather than timing out");
    }

    [Fact]
    public async Task StopAsync_WhileFollowingChanges_LeavesAnIndexAnotherConnectionCanRead()
    {
        using var host = BuildHost();
        var writer = host.Services.GetRequiredService<ISessionIndexWriter>();

        await host.StartAsync();
        await WaitForAsync(writer, stored => stored.Fingerprint.ParseOffset > 0);
        await host.StopAsync();

        CountIndexedSessions().Should().Be(1);
    }

    public void Dispose()
    {
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

    private IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Logging.ClearProviders();
        builder.Services.AddSessionFinderCore(builder.Configuration);
        builder.Services.AddSessionFinderInfrastructure(builder.Configuration);
        builder.Services.AddSessionFinderIndexer();

        builder.Services.Configure<FinderOptions>(options =>
        {
            options.ClaudeConfigDirectory = _configurationDirectory;
            options.IndexPath = _databasePath;
            options.ChangePollMilliseconds = PollMilliseconds;
            options.ChangeSettleMilliseconds = SettleMilliseconds;
            options.ChangeMaximumWaitMilliseconds = MaximumWaitMilliseconds;
        });
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = ShutdownBudget);

        return builder.Build();
    }

    /// <summary>
    /// Waits until the index says what the test is waiting for, or gives up loudly.
    /// </summary>
    private static async Task<IndexedSession> WaitForAsync(
        ISessionIndexWriter writer,
        Func<IndexedSession, bool> isSatisfied)
    {
        SessionId.TryParseFromFileName(SessionIdentifier, out var sessionId);
        var deadline = DateTime.UtcNow + IndexingDeadline;

        while (DateTime.UtcNow < deadline)
        {
            var stored = await writer.GetIndexedSessionAsync(sessionId, CancellationToken.None);

            if (stored is not null && isSatisfied(stored))
            {
                return stored;
            }

            await Task.Delay(BetweenChecks);
        }

        throw new TimeoutException($"The index did not reach the expected state within {IndexingDeadline}.");
    }

    private long WriteAheadLogLength()
    {
        var log = new FileInfo(_databasePath + WriteAheadLogSuffix);

        return log.Exists ? log.Length : 0;
    }

    private long CountIndexedSessions()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sessions;";

        return (long)command.ExecuteScalar()!;
    }
}
