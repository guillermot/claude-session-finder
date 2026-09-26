namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// A program an adapter found on this machine, described in enough detail for the slice to build a
/// command around it without knowing how it was found.
/// </summary>
/// <remarks>
/// The leading arguments exist because the last-resort way to reach an editor is not the editor at
/// all but a shell told to run it. Carrying that as data rather than as a second code path keeps one
/// command-building routine instead of one per way the program was located.
/// </remarks>
public sealed record LaunchProgram
{
    /// <summary>Absolute path of the program, or a name the operating system resolves.</summary>
    public required string Executable { get; init; }

    /// <summary>Arguments that always come before the ones the caller supplies.</summary>
    public IReadOnlyList<string> LeadingArguments { get; init; } = [];

    /// <summary>What should happen to the console window running the program would create.</summary>
    public ConsoleWindowMode ConsoleWindow { get; init; } = ConsoleWindowMode.None;
}
