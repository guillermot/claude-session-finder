namespace SessionFinder.Cli.Commands;

/// <summary>
/// The set of diagnostic commands the CLI head will expose. Handlers are wired in later
/// milestones; the catalog exists now so usage output and dispatch have a single source.
/// </summary>
public static class CliCommandCatalog
{
    /// <summary>
    /// All known commands, in the order they are listed in the usage block.
    /// </summary>
    public static IReadOnlyList<CliCommandDescriptor> All { get; } =
    [
        new("parse", "parse <file>", "Parse one transcript file and print title, folder, chunks and byte offset."),
        new("reindex", "reindex [--full]", "Scan every session root and bring the index up to date."),
        new("search", "search <query>", "Run a search against the index and print ranked results."),
        new("watch", "watch", "Follow session files and index changes as they land."),
        new("status", "status", "Print index counters, parse errors, database size and last run time."),
    ];

    /// <summary>
    /// Finds a command by its verb, ignoring case.
    /// </summary>
    /// <param name="name">The verb typed by the user.</param>
    /// <returns>The matching descriptor, or <see langword="null"/> when the verb is unknown.</returns>
    public static CliCommandDescriptor? Find(string? name) =>
        name is null
            ? null
            : All.FirstOrDefault(command => string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase));
}
