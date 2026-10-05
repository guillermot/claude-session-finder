using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Has a model write a recap up as what to say at a stand-up.
/// </summary>
public interface ISummarizeRecapHandler
{
    /// <summary>
    /// Writes the summary.
    /// </summary>
    /// <param name="command">The recap to summarise.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>The summary, or why none could be written.</returns>
    Task<Result<string>> HandleAsync(SummarizeRecapCommand command, CancellationToken cancellationToken);
}
