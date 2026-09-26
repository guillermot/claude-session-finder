using System.Globalization;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.ReconcileIndex;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// The two things the tray menu can do to the index itself: read every transcript again, and show
/// the user where the logs are.
/// </summary>
/// <remarks>
/// <para>
/// Rebuilding is the answer to "the results look wrong". It reads every transcript from the start
/// rather than from its watermark, which also reclaims the space the rewrite frees, and it is the
/// only pass that does either — an incremental pass must stay cheap enough to run on every change.
/// </para>
/// <para>
/// Showing the log folder goes through the same reveal action a result row uses rather than
/// starting a file manager from a second place. This assembly has no business naming one.
/// </para>
/// </remarks>
/// <param name="reconcile">Reads the transcripts and writes the index.</param>
/// <param name="reveal">Shows a folder in the system file manager.</param>
/// <param name="paths">Where the logs are written.</param>
/// <param name="notifier">How the outcome reaches a user who has no window open.</param>
public sealed class IndexMaintenance(
    IReconcileIndexHandler reconcile,
    IRevealInFileExplorerHandler reveal,
    IApplicationPaths paths,
    IUserNotifier notifier)
{
    private const string RebuiltTitle = "Index rebuilt";

    /// <summary>
    /// Reads every transcript again and replaces what the index holds for it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass between transcripts.</param>
    /// <returns>Success once the pass has finished.</returns>
    public async Task<Result> RebuildIndexAsync(CancellationToken cancellationToken)
    {
        var result = await reconcile
            .HandleAsync(new ReconcileIndexCommand { ForceFullReparse = true }, cancellationToken)
            .ConfigureAwait(false);

        notifier.Notify(UserNotification.Information(RebuiltTitle, Describe(result)));

        return Result.Success();
    }

    /// <summary>
    /// Opens the folder the rolling log files are written to.
    /// </summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the file manager has been started, or why it could not be.</returns>
    public Task<Result> OpenLogFolderAsync(CancellationToken cancellationToken)
    {
        var folder = WorkingFolder.From(paths.LogFolderPath, FolderSource.ApplicationFolder);

        return reveal.HandleAsync(new RevealInFileExplorerCommand(folder), cancellationToken);
    }

    private static string Describe(ReconcileIndexResult result) => string.Create(
        CultureInfo.CurrentCulture,
        $"{result.SessionsIndexed} session(s) read again in {result.Elapsed.TotalSeconds:N1} s.");
}
