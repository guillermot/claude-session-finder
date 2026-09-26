using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Search;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Presentation.Actions;

/// <summary>
/// The four things that can be done to the selected result, and the state that decides which of
/// them are available.
/// </summary>
/// <remarks>
/// <para>
/// This is a separate view model from the search box rather than a handful of extra commands on it,
/// because the two answer different questions: one is about what the list shows, the other about
/// what happens to one row of it. Keeping them apart is also what makes the disabled state
/// assertable — a test can set a target with no folder and read which commands went unavailable,
/// without a window.
/// </para>
/// <para>
/// A session whose folder was never recorded disables four of the six commands. That is a normal
/// session rather than a broken one, so the refusal is a reason the window can show next to the
/// shortcuts, not an exception and not a silent no-op.
/// </para>
/// </remarks>
public sealed partial class SessionActionsViewModel : ObservableObject
{
    private const string OpenInEditorName = "open in editor";
    private const string RevealName = "reveal in file manager";
    private const string ResumeName = "resume session";
    private const string CopyName = "copy";

    private const string OpenInEditorFailureTitle = "Could not open the folder";
    private const string RevealFailureTitle = "Could not show the folder";
    private const string ResumeFailureTitle = "Could not resume the session";
    private const string CopyFailureTitle = "Could not copy";

    private readonly IOpenFolderInEditorHandler _openInEditor;
    private readonly IRevealInFileExplorerHandler _reveal;
    private readonly IResumeSessionHandler _resume;
    private readonly ICopySessionDetailHandler _copy;
    private readonly AppCommandGuard _guard;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenInEditorCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevealInFileManagerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeSessionCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyFolderPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySessionIdCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyResumeCommandLineCommand))]
    [NotifyPropertyChangedFor(nameof(DisabledReason))]
    private SessionResultViewModel? _target;

    /// <summary>
    /// Builds the view model.
    /// </summary>
    /// <param name="openInEditor">Opens a folder in the editor.</param>
    /// <param name="reveal">Shows a folder in the file manager.</param>
    /// <param name="resume">Reopens a session in a terminal.</param>
    /// <param name="copy">Puts a detail of a session on the clipboard.</param>
    /// <param name="guard">The seam every user-initiated action is reported through.</param>
    public SessionActionsViewModel(
        IOpenFolderInEditorHandler openInEditor,
        IRevealInFileExplorerHandler reveal,
        IResumeSessionHandler resume,
        ICopySessionDetailHandler copy,
        AppCommandGuard guard)
    {
        ArgumentNullException.ThrowIfNull(openInEditor);
        ArgumentNullException.ThrowIfNull(reveal);
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(guard);

        _openInEditor = openInEditor;
        _reveal = reveal;
        _resume = resume;
        _copy = copy;
        _guard = guard;
    }

    /// <summary>
    /// Raised after an action has done what it was asked, which is the launcher's cue to get out of
    /// the way.
    /// </summary>
    public event EventHandler? Succeeded;

    /// <summary>
    /// Why most of the actions are unavailable, or <see langword="null"/> when they are not.
    /// </summary>
    public string? DisabledReason =>
        Target is { IsFolderKnown: false } ? AppError.FolderUnknown.Message : null;

    /// <summary>Opens the target's folder in the editor.</summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running action.</returns>
    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    public Task OpenInEditorAsync(CancellationToken cancellationToken) =>
        RunAsync(
            OpenInEditorName,
            OpenInEditorFailureTitle,
            target => _openInEditor.HandleAsync(
                new OpenFolderInEditorCommand(target.WorkingFolder),
                cancellationToken));

    /// <summary>Shows the target's folder in the file manager.</summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running action.</returns>
    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    public Task RevealInFileManagerAsync(CancellationToken cancellationToken) =>
        RunAsync(
            RevealName,
            RevealFailureTitle,
            target => _reveal.HandleAsync(
                new RevealInFileExplorerCommand(target.WorkingFolder),
                cancellationToken));

    /// <summary>Reopens the target session in a terminal, in the folder it was started from.</summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running action.</returns>
    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    public Task ResumeSessionAsync(CancellationToken cancellationToken) =>
        RunAsync(
            ResumeName,
            ResumeFailureTitle,
            target => _resume.HandleAsync(
                new ResumeSessionCommand(target.SessionId, target.WorkingFolder),
                cancellationToken));

    /// <summary>Puts the target's folder path on the clipboard.</summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running action.</returns>
    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    public Task CopyFolderPathAsync(CancellationToken cancellationToken) =>
        CopyAsync(SessionDetail.FolderPath, cancellationToken);

    /// <summary>
    /// Puts the target's session identifier on the clipboard. Available even when the folder is
    /// unknown, because that is the session a user most needs to chase down by hand.
    /// </summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running action.</returns>
    [RelayCommand(CanExecute = nameof(CanAct))]
    public Task CopySessionIdAsync(CancellationToken cancellationToken) =>
        CopyAsync(SessionDetail.SessionId, cancellationToken);

    /// <summary>Puts the whole pasteable resume command line on the clipboard.</summary>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>The running action.</returns>
    [RelayCommand(CanExecute = nameof(CanActOnFolder))]
    public Task CopyResumeCommandLineAsync(CancellationToken cancellationToken) =>
        CopyAsync(SessionDetail.ResumeCommandLine, cancellationToken);

    private bool CanAct() => Target is not null;

    private bool CanActOnFolder() => Target is { IsFolderKnown: true };

    private Task CopyAsync(SessionDetail detail, CancellationToken cancellationToken) =>
        RunAsync(
            CopyName,
            CopyFailureTitle,
            target => _copy.HandleAsync(
                new CopySessionDetailCommand(detail, target.SessionId, target.WorkingFolder),
                cancellationToken));

    /// <summary>
    /// Runs one action against the selected row through the command guard, and announces the
    /// success so the launcher can get out of the way. Reporting a refusal or a failure is the
    /// guard's job, which is why none of it appears here.
    /// </summary>
    private async Task RunAsync(
        string action,
        string failureTitle,
        Func<SessionResultViewModel, Task<Result>> run)
    {
        if (Target is not { } target)
        {
            return;
        }

        var succeeded = await _guard.RunAsync(action, failureTitle, () => run(target)).ConfigureAwait(true);

        if (succeeded)
        {
            Succeeded?.Invoke(this, EventArgs.Empty);
        }
    }
}
