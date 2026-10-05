namespace SessionFinder.Infrastructure.Processes;

/// <summary>
/// How a program run to completion ended.
/// </summary>
public sealed record ProcessOutcome
{
    /// <summary>The exit code, or <c>-1</c> when the program never finished.</summary>
    public int ExitCode { get; init; }

    /// <summary>Everything written to standard output.</summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>Everything written to standard error.</summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>Why the program could not be started, or <see langword="null"/> when it was.</summary>
    public string? StartError { get; init; }

    /// <summary>Whether the program was killed for running past its time.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Whether the program started, finished in time and exited zero.</summary>
    public bool Succeeded => StartError is null && !TimedOut && ExitCode == 0;

    /// <summary>
    /// Builds the outcome of a program that could not be started.
    /// </summary>
    /// <param name="reason">Why, in the operating system's words.</param>
    /// <returns>The outcome.</returns>
    public static ProcessOutcome NotStarted(string reason) => new() { ExitCode = -1, StartError = reason };

    /// <summary>The outcome of a program killed for running past its time.</summary>
    public static ProcessOutcome Expired { get; } = new() { ExitCode = -1, TimedOut = true };
}
