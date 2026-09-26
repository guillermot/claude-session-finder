using System.IO;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Finds the Visual Studio Code executable on this machine.
/// </summary>
/// <remarks>
/// <para>
/// The obvious implementation — run <c>code</c> and let the search path sort it out — does not work
/// and fails in two different ways depending on how it is attempted. What is on the search path is
/// <c>code.cmd</c>, a batch file: starting it without a shell is refused outright, and starting it
/// with one flashes a console window across the screen every time the user opens a folder. So the
/// real executable is found instead, and the batch file is used only as a signpost to it.
/// </para>
/// <para>
/// The last resort does run the batch file through a shell, with the console suppressed, and it is
/// reached only when the batch file exists but the executable is not where it should be relative to
/// it. That is a portable or relocated installation — rare, but the alternative there is to tell a
/// user with a working <c>code</c> command that they have no editor.
/// </para>
/// </remarks>
/// <param name="options">The configured editor path, if the user set one.</param>
internal sealed class VsCodeLocator(IOptionsMonitor<ShellOptions> options) : IEditorLocator
{
    private const string ExecutableName = "Code.exe";
    private const string LauncherName = "code.cmd";
    private const string InstallFolderName = "Microsoft VS Code";
    private const string ProgramsFolderName = "Programs";
    private const string ShellExecutable = "cmd.exe";
    private const string RunAndExitFlag = "/c";
    private const string LauncherCommand = "code";

    /// <inheritdoc />
    public Result<LaunchProgram> Locate()
    {
        var executable = FindConfigured()
            ?? FindUnderLocalApplicationData()
            ?? FindUnderProgramFiles()
            ?? FindBesideLauncher();

        if (executable is not null)
        {
            return Result<LaunchProgram>.Success(new LaunchProgram { Executable = executable });
        }

        return ExecutableSearch.Find(LauncherName) is null
            ? Result<LaunchProgram>.Failure(AppError.EditorNotFound)
            : Result<LaunchProgram>.Success(LaunchThroughShell());
    }

    private string? FindConfigured()
    {
        var configured = options.CurrentValue.EditorPath;

        return !string.IsNullOrWhiteSpace(configured) && File.Exists(configured) ? configured : null;
    }

    private static string? FindUnderLocalApplicationData() => ExistingPath(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProgramsFolderName,
        InstallFolderName,
        ExecutableName);

    private static string? FindUnderProgramFiles() => ExistingPath(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        InstallFolderName,
        ExecutableName);

    /// <summary>
    /// The batch file lives in a <c>bin</c> folder directly beneath the installation, so the
    /// executable is one level above the folder holding it.
    /// </summary>
    private static string? FindBesideLauncher()
    {
        var launcher = ExecutableSearch.Find(LauncherName);

        if (launcher is null)
        {
            return null;
        }

        var installFolder = Path.GetDirectoryName(Path.GetDirectoryName(launcher));

        return installFolder is null ? null : ExistingPath(installFolder, ExecutableName);
    }

    private static LaunchProgram LaunchThroughShell() => new()
    {
        Executable = ShellExecutable,
        LeadingArguments = [RunAndExitFlag, LauncherCommand],
        ConsoleWindow = ConsoleWindowMode.Suppressed,
    };

    private static string? ExistingPath(string root, params string[] segments)
    {
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var candidate = Path.Combine([root, .. segments]);

        return File.Exists(candidate) ? candidate : null;
    }
}
