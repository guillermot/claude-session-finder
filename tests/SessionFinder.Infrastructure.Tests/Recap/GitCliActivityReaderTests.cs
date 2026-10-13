using Microsoft.Extensions.Logging.Abstractions;
using SessionFinder.Infrastructure.Processes;
using SessionFinder.Infrastructure.Recap;

namespace SessionFinder.Infrastructure.Tests.Recap;

public sealed class GitCliActivityReaderTests : IDisposable
{
    private const string Me = "me+recap@example.com";
    private const string SomeoneElse = "someone@example.com";

    private static readonly DateTimeOffset Friday = new(2026, 10, 2, 7, 0, 0, TimeSpan.Zero);

    private readonly string _repository = Path.Combine(Path.GetTempPath(), "session-finder-git", Guid.NewGuid().ToString("n"));
    private readonly ProcessRunner _runner = new();
    private readonly GitCliActivityReader _reader;

    public GitCliActivityReaderTests()
    {
        _reader = new GitCliActivityReader(_runner, NullLogger<GitCliActivityReader>.Instance);
    }

    [Fact]
    public void ParseLog_WellFormedLines_ReadsThemOldestFirst()
    {
        var output = "bbbbbbb\u001f2026-10-02T15:00:00+00:00\u001fSecond\naaaaaaa\u001f2026-10-02T09:00:00+00:00\u001fFirst: with a colon\n";

        var commits = GitCliActivityReader.ParseLog(output, Friday, Friday.AddDays(1));

        commits.Select(commit => commit.Subject).Should().Equal("First: with a colon", "Second");
    }

    [Fact]
    public void ParseLog_AuthoredOutsideTheWindow_IsLeftOut()
    {
        var output = "aaaaaaa\u001f2026-09-30T09:00:00+00:00\u001fOld work, rebased later\n";

        GitCliActivityReader.ParseLog(output, Friday, Friday.AddDays(1)).Should().BeEmpty();
    }

    [Fact]
    public void ParseLog_ATruncatedLine_IsSkipped()
    {
        GitCliActivityReader.ParseLog("aaaaaaa\u001f2026-10-02", Friday, Friday.AddDays(1)).Should().BeEmpty();
    }

    [Fact]
    public async Task FindRepositoryRootAsync_AFolderThatDoesNotExist_IsNotARepository()
    {
        var root = await _reader.FindRepositoryRootAsync(Path.Combine(_repository, "missing"), CancellationToken.None);

        root.Should().BeNull();
    }

    [Fact]
    public async Task FindRepositoryRootAsync_ASubfolderOfARepository_FindsTheTop()
    {
        if (!await TryCreateRepositoryAsync())
        {
            return;
        }

        var subfolder = Directory.CreateDirectory(Path.Combine(_repository, "src")).FullName;

        var root = await _reader.FindRepositoryRootAsync(subfolder, CancellationToken.None);

        Normalize(root!).Should().Be(Normalize(_repository));
    }

    [Fact]
    public async Task GetCommitsAsync_CommitsByTwoAuthors_ReturnsOnlyTheConfiguredUsers()
    {
        if (!await TryCreateRepositoryAsync())
        {
            return;
        }

        await CommitAsync("Mine", Me, Friday.AddHours(2));
        await CommitAsync("Theirs", SomeoneElse, Friday.AddHours(3));

        var commits = await _reader.GetCommitsAsync(_repository, Friday, Friday.AddDays(1), CancellationToken.None);

        commits.Should().ContainSingle().Which.Subject.Should().Be("Mine");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_repository))
            {
                foreach (var file in Directory.EnumerateFiles(_repository, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_repository, recursive: true);
            }
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

    /// <summary>
    /// Creates a repository whose configured user is <see cref="Me"/>. A machine with no git skips
    /// the test rather than failing it: the reader is built for that machine too, and answers it
    /// with no commits.
    /// </summary>
    private async Task<bool> TryCreateRepositoryAsync()
    {
        Directory.CreateDirectory(_repository);

        return await GitAsync("init", "--quiet")
            && await GitAsync("config", "user.email", Me)
            && await GitAsync("config", "user.name", "Me")
            && await GitAsync("config", "commit.gpgsign", "false");
    }

    private async Task CommitAsync(string subject, string author, DateTimeOffset at)
    {
        var date = at.ToString("yyyy-MM-dd'T'HH:mm:ssK", System.Globalization.CultureInfo.InvariantCulture);
        var request = new ProcessRequest
        {
            FileName = "git",
            Arguments = ["-C", _repository, "commit", "--allow-empty", "--quiet", "-m", subject, $"--author=Somebody <{author}>", $"--date={date}"],
            Environment = new Dictionary<string, string> { ["GIT_COMMITTER_DATE"] = date },
            Timeout = TimeSpan.FromSeconds(10),
        };

        var outcome = await _runner.RunAsync(request, CancellationToken.None);
        outcome.Succeeded.Should().BeTrue(outcome.StandardError);
    }

    private async Task<bool> GitAsync(params string[] arguments)
    {
        var outcome = await _runner.RunAsync(
            new ProcessRequest { FileName = "git", Arguments = ["-C", _repository, .. arguments], Timeout = TimeSpan.FromSeconds(10) },
            CancellationToken.None);

        return outcome.Succeeded;
    }

    /// <summary>
    /// The temporary folder is reached through a symbolic link on macOS, and git reports the
    /// resolved path, so both sides are compared after resolving.
    /// </summary>
    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return full.StartsWith("/private/", StringComparison.Ordinal) ? full["/private".Length..] : full;
    }
}
