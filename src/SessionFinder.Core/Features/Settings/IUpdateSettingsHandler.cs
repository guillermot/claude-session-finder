using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Validates and stores the settings.
/// </summary>
public interface IUpdateSettingsHandler
{
    /// <summary>
    /// Writes the settings, or says why they were not written.
    /// </summary>
    /// <param name="command">The values to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the reason the settings were rejected or could not be stored.</returns>
    Task<Result> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken);
}
