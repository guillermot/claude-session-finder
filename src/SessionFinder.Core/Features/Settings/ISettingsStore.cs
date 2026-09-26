namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Writes the settings file the application reads its configuration from.
/// </summary>
/// <remarks>
/// There is no read here on purpose. Reading is what the configuration binder already does, and a
/// second reader would be a second answer to "what is configured" that could disagree with the one
/// every other part of the application is using.
/// </remarks>
public interface ISettingsStore
{
    /// <summary>
    /// Writes the settings, leaving any value in the file the application does not own untouched.
    /// </summary>
    /// <param name="settings">The values to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file has been replaced.</returns>
    Task SaveAsync(FinderSettings settings, CancellationToken cancellationToken);
}
