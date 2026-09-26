namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// The terminal an adapter found on this machine, and which of the two shapes of terminal it is.
/// </summary>
public sealed record TerminalProgram
{
    /// <summary>Absolute path of the terminal, or a name the operating system resolves.</summary>
    public required string Executable { get; init; }

    /// <summary>Which shape of terminal it is, which decides how it is told where to start.</summary>
    public required TerminalKind Kind { get; init; }
}
