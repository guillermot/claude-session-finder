using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Finds session transcripts under the Claude configuration directory.
/// </summary>
/// <remarks>
/// <para>
/// A session transcript is a <c>.jsonl</c> file sitting directly inside a project directory. Each
/// project directory also holds a sibling directory named after a session, and that one contains
/// subagent transcripts, tool results and memory files — measured on a real machine, recursing
/// turns 96 sessions into 152 files, most of which are not sessions at all.
/// </para>
/// <para>
/// Enumerating one level deep is what excludes them, and it does so structurally rather than by
/// pattern-matching folder names that could change.
/// </para>
/// </remarks>
public sealed class ClaudeProjectsCatalog : ISessionFileCatalog
{
    private const string TranscriptSearchPattern = "*.jsonl";

    /// <summary>
    /// Resolves the projects directory from configuration.
    /// </summary>
    /// <param name="options">The bound settings; a blank configuration directory selects the default.</param>
    public ClaudeProjectsCatalog(IOptions<FinderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ProjectsDirectory = Path.GetFullPath(Path.Combine(
            ClaudeConfigurationDirectory.Resolve(options.Value.ClaudeConfigDirectory),
            ClaudeConfigurationDirectory.ProjectsFolderName));
    }

    /// <summary>The directory whose project folders are enumerated.</summary>
    public string ProjectsDirectory { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionFile>> ListSessionFilesAsync(CancellationToken cancellationToken) =>
        Task.Run(() => ListSessionFiles(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<SessionFile?> DescribeAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Task.Run(() => IsInsideAProjectDirectory(filePath) ? TryDescribe(filePath) : null, cancellationToken);
    }

    /// <summary>
    /// Whether the path sits directly inside a project directory, which is what separates a session
    /// transcript from the subagent transcripts kept one level further down.
    /// </summary>
    private bool IsInsideAProjectDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        return directory is not null
            && string.Equals(
                Path.GetDirectoryName(directory),
                ProjectsDirectory,
                StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<SessionFile> ListSessionFiles(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(ProjectsDirectory))
        {
            return [];
        }

        var files = new List<SessionFile>();

        foreach (var projectDirectory in Directory.EnumerateDirectories(ProjectsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectTranscripts(projectDirectory, files);
        }

        files.Sort(NewestFirst);

        return files;
    }

    private static void CollectTranscripts(string projectDirectory, List<SessionFile> files)
    {
        var transcripts = Directory.EnumerateFiles(
            projectDirectory,
            TranscriptSearchPattern,
            SearchOption.TopDirectoryOnly);

        foreach (var transcript in transcripts)
        {
            if (TryDescribe(transcript) is { } sessionFile)
            {
                files.Add(sessionFile);
            }
        }
    }

    /// <summary>
    /// Describes one transcript, or reports nothing when the file is not a session: the file name
    /// is the session identifier, so a name that is not one belongs to something else.
    /// </summary>
    private static SessionFile? TryDescribe(string path)
    {
        if (!SessionId.TryParseFromFileName(path, out var sessionId))
        {
            return null;
        }

        var info = new FileInfo(path);

        if (!info.Exists)
        {
            return null;
        }

        return new SessionFile
        {
            SessionId = sessionId,
            FilePath = info.FullName,
            FileName = info.Name,
            SizeBytes = info.Length,
            LastWriteTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
        };
    }

    /// <summary>
    /// Orders the newest transcript first, so a cold index reaches what the user was last working
    /// on before it reaches anything else.
    /// </summary>
    private static int NewestFirst(SessionFile left, SessionFile right) =>
        right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
}
