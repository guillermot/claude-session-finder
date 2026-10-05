using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Results;
using SessionFinder.Infrastructure.Processes;

namespace SessionFinder.Infrastructure.Recap;

/// <summary>
/// Writes recap summaries with the user's own <c>claude</c> command line in print mode.
/// </summary>
/// <remarks>
/// <para>
/// Going through the command line rather than the API means there is no key to configure: the
/// summary is written under whatever account the user already signed Claude Code in with.
/// </para>
/// <para>
/// The run is made as inert as the command line allows. It has no tools, so the model can only
/// answer; it is not persisted, so the summary never becomes a session of its own that the next
/// recap would then report as work; and it runs in the temporary folder, so no project's settings
/// or instructions are picked up along the way. The prompt goes in on standard input, which keeps
/// it off the command line where other processes could read it.
/// </para>
/// </remarks>
public sealed class ClaudeCliRecapSummarizer(
    IProcessRunner runner,
    IOptionsMonitor<RecapOptions> options,
    ILogger<ClaudeCliRecapSummarizer> logger) : IRecapSummarizer
{
    private const int ReasonMaxLength = 300;
    private const string PathVariable = "PATH";
    private static readonly TimeSpan SummaryTimeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async Task<Result<string>> SummarizeAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var settings = options.CurrentValue;
        var executable = ClaudeExecutableLocator.Find(settings.ClaudeExecutable);

        if (executable is null)
        {
            RecapAdaptersLog.SummarizerMissing(logger);
            return Result<string>.Failure(AppError.SummarizerNotFound);
        }

        var model = string.IsNullOrWhiteSpace(settings.SummaryModel) ? RecapOptions.DefaultSummaryModel : settings.SummaryModel;

        RecapAdaptersLog.SummaryRequested(logger, executable, model);

        var outcome = await runner
            .RunAsync(BuildRequest(executable, model, prompt), cancellationToken)
            .ConfigureAwait(false);

        return Interpret(outcome);
    }

    /// <summary>
    /// Builds the run. The command's own folder is put at the front of the path because an
    /// installation made through a package manager is a script that looks for its runtime beside
    /// itself, on a path the launcher did not inherit.
    /// </summary>
    /// <param name="executable">The command to run.</param>
    /// <param name="model">The model alias or name.</param>
    /// <param name="prompt">The prompt, sent on standard input.</param>
    /// <returns>The request.</returns>
    public static ProcessRequest BuildRequest(string executable, string model, string prompt)
    {
        ArgumentNullException.ThrowIfNull(executable);

        var folder = Path.GetDirectoryName(executable);
        var inherited = Environment.GetEnvironmentVariable(PathVariable) ?? string.Empty;
        var environment = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(folder))
        {
            environment[PathVariable] = string.Concat(folder, Path.PathSeparator.ToString(), inherited);
        }

        return new ProcessRequest
        {
            FileName = executable,
            Arguments =
            [
                "--print",
                "--model", model,
                "--output-format", "text",
                "--no-session-persistence",
                "--tools", string.Empty,
            ],
            StandardInput = prompt,
            WorkingDirectory = Path.GetTempPath(),
            Environment = environment,
            Timeout = SummaryTimeout,
        };
    }

    private Result<string> Interpret(ProcessOutcome outcome)
    {
        if (!outcome.Succeeded)
        {
            RecapAdaptersLog.SummaryProcessFailed(logger, outcome.ExitCode, outcome.TimedOut, outcome.StartError);

            return Result<string>.Failure(AppError.SummaryFailed(DescribeFailure(outcome)));
        }

        var summary = outcome.StandardOutput.Trim();

        return summary.Length == 0
            ? Result<string>.Failure(AppError.SummaryFailed("the reply was empty."))
            : Result<string>.Success(summary);
    }

    /// <summary>
    /// Says why, in the command's own words where it gave any. Those words are an error message
    /// rather than transcript text — the prompt is never echoed — so they are safe to show.
    /// </summary>
    private static string DescribeFailure(ProcessOutcome outcome)
    {
        if (outcome.StartError is { } startError)
        {
            return $"claude could not be started ({startError}).";
        }

        if (outcome.TimedOut)
        {
            return $"claude did not answer within {SummaryTimeout.TotalMinutes:0} minutes.";
        }

        var said = string.IsNullOrWhiteSpace(outcome.StandardError) ? outcome.StandardOutput : outcome.StandardError;
        var firstLine = said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

        if (string.IsNullOrEmpty(firstLine))
        {
            return $"claude stopped with exit code {outcome.ExitCode}.";
        }

        return firstLine.Length <= ReasonMaxLength ? firstLine : firstLine[..ReasonMaxLength] + "…";
    }
}
