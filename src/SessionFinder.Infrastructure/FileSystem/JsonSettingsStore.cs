using System.Text.Json;
using System.Text.Json.Nodes;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.Settings;

namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Stores the settings in the same JSON file the host reads its configuration from.
/// </summary>
/// <remarks>
/// <para>
/// The file is patched rather than rewritten. It is a configuration file a user is free to edit by
/// hand, and it may hold values this window does not offer — the Claude configuration directory,
/// the index path, the indexer's timings. Serialising the settings over the top of it would
/// silently delete them, and a settings window that eats settings is worse than one that offers
/// fewer.
/// </para>
/// <para>
/// The replacement is atomic: the new contents go to a temporary file beside the target and are
/// moved over it. A configuration file caught half-written by the host's own reload is a file the
/// application then fails to parse, and the window that could fix it reads the same file.
/// </para>
/// </remarks>
/// <param name="paths">Where the settings file lives.</param>
public sealed class JsonSettingsStore(IApplicationPaths paths) : ISettingsStore
{
    private const string TemporaryFileSuffix = ".saving";
    private const char SectionSeparator = ':';

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <inheritdoc />
    public async Task SaveAsync(FinderSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var filePath = paths.SettingsFilePath;
        var document = await ReadExistingAsync(filePath, cancellationToken).ConfigureAwait(false);

        ApplyShell(document, settings);
        ApplySearch(document, settings);
        ApplyLogLevel(document, settings);
        ApplyRecap(document, settings);

        await ReplaceAsync(filePath, document, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonObject> ReadExistingAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var text = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        return ParseOrEmpty(text);
    }

    /// <summary>
    /// A settings file that is not valid JSON is replaced rather than mourned. The application has
    /// been running on defaults since it started, so there is nothing in the file to preserve, and
    /// refusing to save would leave the user with no way to fix it from the window.
    /// </summary>
    private static JsonObject ParseOrEmpty(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(text, nodeOptions: null, ReadOptions) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void ApplyShell(JsonObject document, FinderSettings settings)
    {
        var section = ShellOptions.SectionName;

        Set(document, $"{section}:{nameof(ShellOptions.Hotkey)}", settings.Hotkey);
        Set(document, $"{section}:{nameof(ShellOptions.StartAtLogin)}", settings.StartAtLogin);
        Set(document, $"{section}:{nameof(ShellOptions.HideOnDeactivate)}", settings.HideOnDeactivate);
        SetOrRemove(document, $"{section}:{nameof(ShellOptions.EditorPath)}", settings.EditorPath);
        SetOrRemove(document, $"{section}:{nameof(ShellOptions.TerminalPath)}", settings.TerminalPath);
    }

    private static void ApplySearch(JsonObject document, FinderSettings settings)
    {
        var section = SearchOptions.SectionName;
        var weights = settings.ChunkWeights;

        Set(document, $"{section}:{nameof(SearchOptions.MaxResults)}", settings.MaxResults);
        Set(document, $"{section}:{nameof(SearchOptions.RecencyWeight)}", settings.RecencyWeight);
        Set(document, $"{section}:{nameof(SearchOptions.RecencyHalfLifeDays)}", settings.RecencyHalfLifeDays);
        Set(document, $"{section}:{nameof(SearchOptions.TitleWeight)}", weights.Title);
        Set(document, $"{section}:{nameof(SearchOptions.FolderWeight)}", weights.Folder);
        Set(document, $"{section}:{nameof(SearchOptions.LastPromptWeight)}", weights.LastPrompt);
        Set(document, $"{section}:{nameof(SearchOptions.UserPromptWeight)}", weights.UserPrompt);
        Set(document, $"{section}:{nameof(SearchOptions.AssistantTextWeight)}", weights.AssistantText);
    }

    private static void ApplyLogLevel(JsonObject document, FinderSettings settings)
    {
        var level = settings.VerboseLogging ? LogLevelOptions.VerboseLevel : LogLevelOptions.NormalLevel;

        Set(document, $"{LogLevelOptions.SectionName}:{nameof(LogLevelOptions.Default)}", level);
    }

    private static void ApplyRecap(JsonObject document, FinderSettings settings)
    {
        var section = RecapOptions.SectionName;

        Set(document, $"{section}:{nameof(RecapOptions.DayStartHour)}", settings.RecapDayStartHour);
        Set(document, $"{section}:{nameof(RecapOptions.LookbackDays)}", settings.RecapLookbackDays);
        Set(document, $"{section}:{nameof(RecapOptions.IncludeGit)}", settings.RecapIncludeGit);
        Set(document, $"{section}:{nameof(RecapOptions.EnableAiSummary)}", settings.RecapAiSummary);
    }

    private static void Set<TValue>(JsonObject document, string path, TValue value) =>
        Write(document, path, JsonValue.Create(value));

    private static void SetOrRemove(JsonObject document, string path, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Write(document, path, node: null);
            return;
        }

        Write(document, path, JsonValue.Create(value));
    }

    /// <summary>
    /// Writes one value at a colon-separated configuration path, creating the objects along the
    /// way and replacing anything that is not an object, which is what a hand-edited file can hold.
    /// A null node removes the key rather than storing a null.
    /// </summary>
    private static void Write(JsonObject document, string path, JsonNode? node)
    {
        var segments = path.Split(SectionSeparator);
        var parent = document;

        foreach (var segment in segments[..^1])
        {
            if (parent[segment] is not JsonObject child)
            {
                child = [];
                parent[segment] = child;
            }

            parent = child;
        }

        var leaf = segments[^1];

        if (node is null)
        {
            parent.Remove(leaf);
            return;
        }

        parent[leaf] = node;
    }

    private static async Task ReplaceAsync(string filePath, JsonObject document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var temporaryPath = filePath + TemporaryFileSuffix;

        await File.WriteAllTextAsync(
            temporaryPath,
            document.ToJsonString(WriteOptions),
            cancellationToken).ConfigureAwait(false);

        File.Move(temporaryPath, filePath, overwrite: true);
    }
}
