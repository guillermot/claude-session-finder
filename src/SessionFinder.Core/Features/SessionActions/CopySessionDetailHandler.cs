using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Puts one piece of a session on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// Two of the three details need a folder and one does not, which is why the precondition is
/// per-detail here rather than at the top of the method: an identifier stays copyable from a
/// session whose folder was never recorded, and that is exactly the session a user is most likely
/// to want to look up by hand.
/// </para>
/// <para>
/// The resume line is built by the same factory that builds the command a resume actually runs.
/// This is the one path where that factory is not reached through a launcher, and it is the reason
/// the factory is a port at all: the text copied here is pasted into a shell by hand, so it has to
/// be in the dialect of the machine the user is sitting at.
/// </para>
/// </remarks>
/// <param name="clipboard">Where the text goes.</param>
/// <param name="commands">Renders the pasteable resume line.</param>
public sealed class CopySessionDetailHandler(
    IClipboardService clipboard,
    ITerminalCommandFactory commands) : ICopySessionDetailHandler
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(
        CopySessionDetailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var text = BuildText(command);

        if (text.IsFailure)
        {
            return Result.Failure(text.Error!);
        }

        return await clipboard.SetTextAsync(text.Value, cancellationToken).ConfigureAwait(false);
    }

    private Result<string> BuildText(CopySessionDetailCommand command) => command.Detail switch
    {
        SessionDetail.SessionId => Result<string>.Success(command.SessionId.ToString()),
        SessionDetail.FolderPath => RequireFolder(command, folder => folder.Display),
        _ => RequireFolder(
            command,
            folder => commands.BuildResumeCommandLine(folder, command.SessionId)),
    };

    private static Result<string> RequireFolder(
        CopySessionDetailCommand command,
        Func<WorkingFolder, string> build)
    {
        var precondition = FolderPrecondition.Check(command.Folder);

        return precondition.IsFailure
            ? Result<string>.Failure(precondition.Error!)
            : Result<string>.Success(build(command.Folder));
    }
}
