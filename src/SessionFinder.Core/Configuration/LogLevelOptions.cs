namespace SessionFinder.Core.Configuration;

/// <summary>
/// The minimum level the logging framework itself is configured with.
/// </summary>
/// <remarks>
/// <para>
/// This binds the framework's own <c>Logging:LogLevel</c> section rather than inventing a setting
/// beside it. Doing so is what makes the verbose-logging switch take effect without a restart for
/// free: the logging infrastructure already watches that section for changes, so the settings slice
/// only has to write the value a user would otherwise have had to know the name of.
/// </para>
/// <para>
/// The property is a string because that is what the section holds. Turning it into the switch the
/// user sees is the settings slice's job.
/// </para>
/// </remarks>
public sealed class LogLevelOptions
{
    /// <summary>Configuration section the options are bound from.</summary>
    public const string SectionName = "Logging:LogLevel";

    /// <summary>The level written while verbose logging is on.</summary>
    public const string VerboseLevel = "Debug";

    /// <summary>The level written while verbose logging is off.</summary>
    public const string NormalLevel = "Information";

    /// <summary>
    /// The minimum level for categories with no rule of their own. Blank means the framework
    /// default, which is the same as <see cref="NormalLevel"/> for this application.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Whether the configured level records more than the ordinary account of what happened.
    /// </summary>
    public bool IsVerbose =>
        string.Equals(Default, VerboseLevel, StringComparison.OrdinalIgnoreCase);
}
