namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// Asks for the settings as they currently apply, together with the paths the window reports.
/// </summary>
public sealed record GetSettingsQuery
{
    /// <summary>The only shape this query has.</summary>
    public static GetSettingsQuery Instance { get; } = new();
}
