using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SessionFinder.Core.Features.DailyRecap;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Prints what was done on the last active day, and on the active days before it, as Markdown.
/// </summary>
/// <remarks>
/// The output is the same text the recap window shows and copies, so it can be piped straight into
/// a chat message. <c>--summarize</c> is the explicit request that lets the recap leave the machine
/// for Claude; without it, nothing does.
/// </remarks>
internal static class RecapCommand
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitUsage = 64;
    private const string DateFlag = "--date";
    private const string DaysFlag = "--days";
    private const string NoGitFlag = "--no-git";
    private const string PromptsFlag = "--prompts";
    private const string SummarizeFlag = "--summarize";
    private const string DateFormat = "yyyy-MM-dd";
    private const string UsageText =
        "Usage: finder recap [--date yyyy-MM-dd] [--days <n>] [--no-git] [--prompts] [--summarize]";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">Arguments after the verb.</param>
    /// <param name="cancellationToken">Cancels the read and the summary.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryReadArguments(arguments, out var options))
        {
            await Console.Error.WriteLineAsync(UsageText).ConfigureAwait(false);
            return ExitUsage;
        }

        using var host = CliHost.Build();
        var recapHandler = host.Services.GetRequiredService<IGetDailyRecapHandler>();

        var recap = await recapHandler.HandleAsync(options.Query, cancellationToken).ConfigureAwait(false);

        Console.Write(RecapMarkdownFormatter.Format(recap, options.IncludePrompts));

        if (!options.Summarize)
        {
            return ExitSuccess;
        }

        var summarizer = host.Services.GetRequiredService<ISummarizeRecapHandler>();
        var summary = await summarizer
            .HandleAsync(new SummarizeRecapCommand(recap), cancellationToken)
            .ConfigureAwait(false);

        if (summary.Error is { } error)
        {
            await Console.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
            return ExitFailure;
        }

        Console.WriteLine();
        Console.WriteLine("---");
        Console.WriteLine();
        Console.WriteLine(summary.Value);

        return ExitSuccess;
    }

    private static bool TryReadArguments(IReadOnlyList<string> arguments, out RecapArguments options)
    {
        options = new RecapArguments { Query = GetDailyRecapQuery.LastActiveDay };

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index].ToLowerInvariant())
            {
                case NoGitFlag:
                    options = options with { Query = options.Query with { IncludeGit = false } };
                    break;
                case PromptsFlag:
                    options = options with { IncludePrompts = true };
                    break;
                case SummarizeFlag:
                    options = options with { Summarize = true };
                    break;
                case DateFlag when TryReadValue(arguments, ref index, out var value)
                    && DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day):
                    options = options with { Query = options.Query with { Day = day } };
                    break;
                case DaysFlag when TryReadValue(arguments, ref index, out var value)
                    && int.TryParse(value, CultureInfo.InvariantCulture, out var days)
                    && days >= 0:
                    options = options with { Query = options.Query with { LookbackDays = days } };
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryReadValue(IReadOnlyList<string> arguments, ref int index, out string value)
    {
        value = string.Empty;
        index++;

        if (index >= arguments.Count)
        {
            return false;
        }

        value = arguments[index];
        return true;
    }

    /// <summary>
    /// What the command line asked for.
    /// </summary>
    private readonly record struct RecapArguments
    {
        public GetDailyRecapQuery Query { get; init; }

        public bool IncludePrompts { get; init; }

        public bool Summarize { get; init; }
    }
}
