namespace SessionFinder.Core.Abstractions;

/// <summary>
/// The three per-user locations the application owns: its settings file, its index and its logs.
/// </summary>
/// <remarks>
/// These are resolved in one place rather than recomputed wherever they are needed, because two
/// parts of the application disagreeing about where the settings file is would be invisible until
/// the moment a saved setting failed to take effect. The split between roaming settings and local
/// index and logs is deliberate: the settings are small and worth carrying between machines, the
/// index is a rebuildable cache and the logs are about one machine.
/// </remarks>
public interface IApplicationPaths
{
    /// <summary>Absolute path of the settings file, whether or not it exists yet.</summary>
    string SettingsFilePath { get; }

    /// <summary>Absolute path of the index database, whether or not it exists yet.</summary>
    string IndexFilePath { get; }

    /// <summary>Absolute path of the folder the rolling log files are written to.</summary>
    string LogFolderPath { get; }
}
