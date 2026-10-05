namespace SessionFinder.Infrastructure.Processes;

/// <summary>
/// Runs a program to completion and hands back what it printed.
/// </summary>
/// <remarks>
/// This is the opposite of the shell launcher the result actions use, which starts a program and
/// lets it go. Asking git for a log and asking Claude for a summary both need the output, a bound
/// on how long to wait for it, and the certainty that the child is gone when the wait ends.
/// </remarks>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the program.
    /// </summary>
    /// <param name="request">What to run, with what input, for how long at most.</param>
    /// <param name="cancellationToken">Kills the program and abandons the wait.</param>
    /// <returns>
    /// What happened, including a program that could not be started or ran out of time. Only a
    /// cancellation is thrown.
    /// </returns>
    Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}
