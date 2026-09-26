using System.Diagnostics;
using System.Globalization;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.Transcripts;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Parses one transcript file and prints what the indexer would have learned from it.
/// </summary>
/// <remarks>
/// This is how the parser is verified against real transcripts without a database or a window:
/// title, folder, chunk counts, the byte offset the next pass would resume from, and how long it
/// took. Chunk text is never printed — transcripts are private working material.
/// </remarks>
internal static class ParseCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int ExitNoInput = 66;
    private const int LabelWidth = 14;
    private const int FileBufferSize = 64 * 1024;

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">Arguments after the verb; exactly one path is expected.</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != 1)
        {
            await Console.Error.WriteLineAsync("Usage: finder parse <file>").ConfigureAwait(false);
            return ExitUsage;
        }

        var path = Path.GetFullPath(arguments[0]);

        if (!File.Exists(path))
        {
            await Console.Error.WriteLineAsync($"No such transcript file: {path}").ConfigureAwait(false);
            return ExitNoInput;
        }

        await ParseAndReportAsync(path, cancellationToken).ConfigureAwait(false);
        return ExitSuccess;
    }

    private static async Task ParseAndReportAsync(string path, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        SessionId.TryParseFromFileName(fileName, out var sessionId);

        await using var stream = OpenForSharedRead(path);
        var parser = new JsonlSessionTranscriptParser();
        var request = new TranscriptParseRequest
        {
            SessionId = sessionId,
            FileName = fileName,
            Content = stream,
        };

        var stopwatch = Stopwatch.StartNew();
        var document = await parser.ParseAsync(request, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        WriteReport(path, stream.Length, document, stopwatch.Elapsed);
    }

    /// <summary>
    /// Opens the transcript the way the indexer has to: Claude keeps its own handle on the file
    /// and may delete it, so denying either share mode breaks on live sessions.
    /// </summary>
    private static FileStream OpenForSharedRead(string path) =>
        new(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = FileBufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });

    private static void WriteReport(string path, long fileLength, SessionDocument document, TimeSpan elapsed)
    {
        WriteLine("File", path);
        WriteLine("Size", Invariant($"{fileLength:N0} bytes"));
        WriteLine("Session", document.SessionId.ToString());
        WriteLine("Title", $"{document.Title.Text}  [{document.Title.Source}]");
        WriteLine("Folder", FormatFolder(document.Folder));
        WriteLine("Folder key", document.Folder.IsKnown ? document.Folder.Key : "-");
        WriteLine("Branch", document.GitBranch ?? "-");
        WriteLine("Activity", FormatActivity(document));
        WriteLine("Messages", document.MessageCount.ToString(CultureInfo.InvariantCulture));

        WriteChunkCounts(document);

        WriteLine("Offset", FormatOffset(document.ParseOffset, fileLength));
        WriteLine("Parse errors", document.ParseError ?? "none");
        WriteLine("Elapsed", Invariant($"{elapsed.TotalMilliseconds:N1} ms"));
    }

    private static void WriteChunkCounts(SessionDocument document)
    {
        WriteLine("Chunks", document.Chunks.Count.ToString(CultureInfo.InvariantCulture));

        var byKind = document.Chunks
            .GroupBy(chunk => chunk.Kind)
            .OrderBy(group => group.Key)
            .Select(group => Invariant($"{group.Key,-14} {group.Count(),6:N0}"));

        foreach (var line in byKind)
        {
            Console.WriteLine($"{string.Empty,LabelWidth}  {line}");
        }
    }

    private static string FormatFolder(WorkingFolder folder) =>
        folder.IsKnown ? $"{folder.Display}  [{folder.Source}]" : $"-  [{folder.Source}]";

    private static string FormatActivity(SessionDocument document)
    {
        if (document.FirstActivity is not { } first || document.LastActivity is not { } last)
        {
            return "-";
        }

        return $"{Format(first)}  ..  {Format(last)}";

        static string Format(DateTimeOffset value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reports the offset against the file length. On a transcript whose last line is complete
    /// these must be equal; anything else means bytes were left unread and has to be explained.
    /// </summary>
    private static string FormatOffset(long parseOffset, long fileLength)
    {
        var verdict = parseOffset == fileLength
            ? "complete"
            : Invariant($"{fileLength - parseOffset:N0} byte(s) left for the next pass");

        return Invariant($"{parseOffset:N0} / {fileLength:N0}  ({verdict})");
    }

    private static void WriteLine(string label, string value) =>
        Console.WriteLine($"{label,-LabelWidth}: {value}");

    /// <summary>
    /// Formats in the invariant culture so a diagnostic report pasted into a review reads the same
    /// on every machine.
    /// </summary>
    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
