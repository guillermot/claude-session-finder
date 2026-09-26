namespace SessionFinder.Core.Features.Settings;

/// <summary>
/// The answer to <see cref="GetSettingsQuery"/>: the editable values and the three locations the
/// window shows but cannot change.
/// </summary>
/// <param name="Settings">The values as they currently apply.</param>
/// <param name="SettingsFilePath">Where the values are stored.</param>
/// <param name="IndexFilePath">Where the index lives. Changing it needs a restart.</param>
/// <param name="LogFolderPath">Where the rolling log files are written.</param>
public sealed record GetSettingsResult(
    FinderSettings Settings,
    string SettingsFilePath,
    string IndexFilePath,
    string LogFolderPath);
