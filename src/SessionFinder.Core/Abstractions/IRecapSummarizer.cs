using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Turns a recap into prose a person would say out loud.
/// </summary>
/// <remarks>
/// This is the only port in the application that sends transcript text off the machine, which is
/// why the window offers it only once the user has switched it on.
/// </remarks>
public interface IRecapSummarizer
{
    /// <summary>
    /// Asks the model to answer the prompt.
    /// </summary>
    /// <param name="prompt">The instructions and the recap they apply to.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>The summary, or why none could be written.</returns>
    Task<Result<string>> SummarizeAsync(string prompt, CancellationToken cancellationToken);
}
