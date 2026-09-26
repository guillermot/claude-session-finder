namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Reads the settings as they currently apply.
/// </summary>
public interface IGetSettingsHandler
{
    /// <summary>
    /// Answers with the editable values and the locations the window reports.
    /// </summary>
    /// <param name="query">The request.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The settings and the paths.</returns>
    Task<GetSettingsResult> HandleAsync(GetSettingsQuery query, CancellationToken cancellationToken);
}
