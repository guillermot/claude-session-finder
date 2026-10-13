namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Asks git what the user committed, which is the strongest evidence there is of what a day of
/// work produced.
/// </summary>
/// <remarks>
/// Every failure here is an absence rather than an error. A folder that has since been deleted, a
/// folder that was never a repository and a machine with no git at all are all ordinary, and each
/// one simply contributes no commits to the recap.
/// </remarks>
public interface IGitActivityReader
{
    /// <summary>
    /// Finds the top of the working tree a folder belongs to.
    /// </summary>
    /// <param name="folder">An absolute folder path.</param>
    /// <param name="cancellationToken">Abandons the lookup.</param>
    /// <returns>The repository root, or <see langword="null"/> when the folder is not inside one.</returns>
    Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the commits the repository's configured user authored inside a window, on any branch.
    /// </summary>
    /// <param name="repositoryRoot">A root returned by <see cref="FindRepositoryRootAsync"/>.</param>
    /// <param name="from">Start of the window, inclusive.</param>
    /// <param name="to">End of the window, exclusive.</param>
    /// <param name="cancellationToken">Abandons the lookup.</param>
    /// <returns>The commits, oldest first, or none when git could not answer.</returns>
    Task<IReadOnlyList<GitCommit>> GetCommitsAsync(
        string repositoryRoot,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
