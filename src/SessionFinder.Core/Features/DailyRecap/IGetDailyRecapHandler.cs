namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Puts together what was done on a working day, for a stand-up.
/// </summary>
public interface IGetDailyRecapHandler
{
    /// <summary>
    /// Builds the recap.
    /// </summary>
    /// <param name="query">Which day, how far back, and whether to ask git.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>The recap; an index with nothing in it yields an empty one rather than an error.</returns>
    Task<DailyRecapResult> HandleAsync(GetDailyRecapQuery query, CancellationToken cancellationToken);
}
