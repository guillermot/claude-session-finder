using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Builds the prompt for a recap and hands it to the summarizer.
/// </summary>
/// <remarks>
/// Whether a summary may be asked for at all is decided by whoever calls this: the window shows the
/// command only once the setting is on, and the command line asks with an explicit flag. Either is
/// the user's own consent, given at the moment it applies.
/// </remarks>
public sealed class SummarizeRecapHandler(
    IRecapSummarizer summarizer,
    ILogger<SummarizeRecapHandler> logger) : ISummarizeRecapHandler
{
    /// <inheritdoc />
    public async Task<Result<string>> HandleAsync(SummarizeRecapCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.Recap.HasActivity)
        {
            return Fail(AppError.NothingToSummarize);
        }

        var result = await summarizer
            .SummarizeAsync(RecapSummaryPrompt.Build(command.Recap), cancellationToken)
            .ConfigureAwait(false);

        if (result.Error is { } error)
        {
            return Fail(error);
        }

        DailyRecapLog.SummaryWritten(logger, result.Value.Length);

        return result;
    }

    private Result<string> Fail(AppError error)
    {
        DailyRecapLog.SummaryFailed(logger, error.Code);

        return Result<string>.Failure(error);
    }
}
