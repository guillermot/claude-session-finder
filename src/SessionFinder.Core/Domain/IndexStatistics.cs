namespace SessionFinder.Core.Domain;

/// <summary>
/// A snapshot of the index: how much is in it, how much of it is damaged, and when it was last
/// brought up to date.
/// </summary>
public sealed record IndexStatistics
{
    /// <summary>Absolute path of the index database, whether or not it exists yet.</summary>
    public required string DatabasePath { get; init; }

    /// <summary>
    /// Whether a usable database is present. A missing file and a file holding an unreadable
    /// schema version are both reported as absent: the index is a cache and is simply rebuilt.
    /// </summary>
    public required bool DatabaseExists { get; init; }

    /// <summary>Size of the database file in bytes.</summary>
    public required long DatabaseSizeBytes { get; init; }

    /// <summary>Number of indexed sessions.</summary>
    public required int SessionCount { get; init; }

    /// <summary>Number of indexed chunks across every session.</summary>
    public required int ChunkCount { get; init; }

    /// <summary>Chunk counts broken down by what the text is.</summary>
    public required IReadOnlyDictionary<ChunkKind, int> ChunksByKind { get; init; }

    /// <summary>Sessions whose last pass had to skip one or more damaged lines.</summary>
    public required int SessionsWithParseErrors { get; init; }

    /// <summary>
    /// Sessions whose working folder could not be recovered. This is the number that says how
    /// well folder recovery is actually doing, so it belongs in the status output.
    /// </summary>
    public required int SessionsWithUnknownFolder { get; init; }

    /// <summary>When a reconcile pass last completed.</summary>
    public DateTimeOffset? LastReconcileAt { get; init; }

    /// <summary>The most recent activity timestamp across every indexed session.</summary>
    public DateTimeOffset? NewestActivity { get; init; }

    /// <summary>
    /// Describes an index that has not been built yet.
    /// </summary>
    /// <param name="databasePath">Where the database would be created.</param>
    /// <returns>An all-zero snapshot reporting the database as absent.</returns>
    public static IndexStatistics Empty(string databasePath) => new()
    {
        DatabasePath = databasePath,
        DatabaseExists = false,
        DatabaseSizeBytes = 0,
        SessionCount = 0,
        ChunkCount = 0,
        ChunksByKind = new Dictionary<ChunkKind, int>(),
        SessionsWithParseErrors = 0,
        SessionsWithUnknownFolder = 0,
    };
}
