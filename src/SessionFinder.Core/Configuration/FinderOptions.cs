namespace SessionFinder.Core.Configuration;

/// <summary>
/// The settings a head project binds from configuration. Every value is optional: a blank value
/// means "work out the conventional location", which is what makes the tool run with no settings
/// file at all.
/// </summary>
public sealed class FinderOptions
{
    /// <summary>Configuration section the options are bound from.</summary>
    public const string SectionName = "Finder";

    /// <summary>How often the indexer looks for changes that have become worth acting on.</summary>
    public const int DefaultChangePollMilliseconds = 500;

    /// <summary>How long a transcript must go unwritten before it is treated as finished.</summary>
    public const int DefaultChangeSettleMilliseconds = 2_000;

    /// <summary>How long a transcript that keeps being written may be held back.</summary>
    public const int DefaultChangeMaximumWaitMilliseconds = 20_000;

    /// <summary>
    /// The Claude configuration directory holding <c>projects</c>. Blank falls back to the
    /// <c>CLAUDE_CONFIG_DIR</c> environment variable and then to <c>~/.claude</c>.
    /// </summary>
    public string? ClaudeConfigDirectory { get; set; }

    /// <summary>
    /// Absolute path of the index database. Blank falls back to the per-user local application
    /// data folder. The index is a rebuildable cache, which is why it does not live beside the
    /// settings file.
    /// </summary>
    public string? IndexPath { get; set; }

    /// <summary>How often the indexer looks for changes that have become worth acting on.</summary>
    public int ChangePollMilliseconds { get; set; } = DefaultChangePollMilliseconds;

    /// <summary>
    /// How long a transcript must go unwritten before it is treated as finished. Short enough that
    /// a session you just closed is searchable almost at once, long enough that the indexer is not
    /// woken by every keystroke of a reply being streamed into the file.
    /// </summary>
    public int ChangeSettleMilliseconds { get; set; } = DefaultChangeSettleMilliseconds;

    /// <summary>
    /// How long a transcript that keeps being written may be held back. This is what makes the
    /// session you are working in right now appear in the index at all: it is never quiet, so
    /// without a limit on waiting for quiet it would never be read.
    /// </summary>
    public int ChangeMaximumWaitMilliseconds { get; set; } = DefaultChangeMaximumWaitMilliseconds;
}
