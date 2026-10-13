namespace SessionFinder.Infrastructure.Processes;

/// <summary>
/// One program to run to completion.
/// </summary>
public sealed record ProcessRequest
{
    /// <summary>The executable, as a path or a name to find on the path.</summary>
    public required string FileName { get; init; }

    /// <summary>The arguments, each passed as one argument with no shell in between.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>The folder to run in, or <see langword="null"/> for this process's own.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Text written to the program's standard input before it is closed.</summary>
    public string? StandardInput { get; init; }

    /// <summary>Variables set in the program's environment, on top of the inherited ones.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>How long the program may run before it is killed.</summary>
    public required TimeSpan Timeout { get; init; }
}
