namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Asks for the settings file to be replaced with these values.
/// </summary>
public sealed record UpdateSettingsCommand
{
    /// <summary>The values to store.</summary>
    public required FinderSettings Settings { get; init; }
}
