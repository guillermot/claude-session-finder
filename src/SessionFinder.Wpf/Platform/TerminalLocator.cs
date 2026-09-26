using System.IO;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Finds the terminal a resumed session is opened in.
/// </summary>
/// <remarks>
/// <para>
/// Windows Terminal is preferred because it is the one terminal here that can be told which
/// directory to start in as an argument, which matters more than appearance: a resume only finds
/// its session when it runs from the directory the session was started in.
/// </para>
/// <para>
/// The fallback is the command shell rather than PowerShell. The command being run is a batch shim,
/// which the command shell runs by name with no quoting rules to get wrong and no execution policy
/// to be blocked by, and the command shell is part of the operating system rather than something
/// that can be absent. PowerShell would be a third shape of command line for no gain over a shell
/// that is guaranteed to be there.
/// </para>
/// </remarks>
/// <param name="options">The configured terminal path, if the user set one.</param>
internal sealed class TerminalLocator(IOptionsMonitor<ShellOptions> options) : ITerminalLocator
{
    private const string TerminalHostName = "wt.exe";
    private const string CommandShellName = "cmd.exe";
    private const string ComSpecVariable = "ComSpec";

    /// <inheritdoc />
    public Result<TerminalProgram> Locate()
    {
        var configured = options.CurrentValue.TerminalPath;

        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return Result<TerminalProgram>.Success(Describe(configured));
        }

        var terminalHost = ExecutableSearch.Find(TerminalHostName);

        if (terminalHost is not null)
        {
            return Result<TerminalProgram>.Success(Describe(terminalHost));
        }

        var commandShell = FindCommandShell();

        return commandShell is null
            ? Result<TerminalProgram>.Failure(AppError.TerminalNotFound)
            : Result<TerminalProgram>.Success(Describe(commandShell));
    }

    /// <summary>
    /// The shape of a terminal is decided by its file name, which is what lets a path the user
    /// configured point at either kind without a second setting to say which it is.
    /// </summary>
    private static TerminalProgram Describe(string executable) => new()
    {
        Executable = executable,
        Kind = IsTerminalHost(executable) ? TerminalKind.TerminalHost : TerminalKind.CommandShell,
    };

    private static bool IsTerminalHost(string executable) =>
        string.Equals(Path.GetFileName(executable), TerminalHostName, StringComparison.OrdinalIgnoreCase);

    private static string? FindCommandShell()
    {
        var comSpec = Environment.GetEnvironmentVariable(ComSpecVariable);

        if (!string.IsNullOrWhiteSpace(comSpec) && File.Exists(comSpec))
        {
            return comSpec;
        }

        var system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), CommandShellName);

        return File.Exists(system) ? system : ExecutableSearch.Find(CommandShellName);
    }
}
