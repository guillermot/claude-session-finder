using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Finds the terminal a resumed session is shown in on a Mac.
/// </summary>
/// <remarks>
/// <para>
/// What is returned is the name AppleScript knows the application by, not a path to a binary. The
/// terminal is never started directly on this platform — it is asked, through <c>osascript</c>, to
/// run a line — so a path would be the one thing the caller could not use.
/// </para>
/// <para>
/// iTerm wins when it is installed, on the same reasoning that prefers Windows Terminal to the
/// command shell: somebody who installed a terminal of their own meant to use it.
/// </para>
/// </remarks>
/// <param name="options">The settings, for an explicitly configured terminal.</param>
internal sealed class MacTerminalLocator(IOptionsMonitor<ShellOptions> options) : ITerminalLocator
{
    private const string ITermBundlePath = "/Applications/iTerm.app";
    private const string ITermApplicationName = "iTerm";
    private const string TerminalApplicationName = "Terminal";
    private const string ApplicationBundleExtension = ".app";

    /// <inheritdoc />
    public Result<TerminalProgram> Locate()
    {
        var configured = options.CurrentValue.TerminalPath;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Configured(configured);
        }

        return Directory.Exists(ITermBundlePath)
            ? Found(ITermApplicationName, TerminalKind.MacITerm)
            : Found(TerminalApplicationName, TerminalKind.MacTerminalApp);
    }

    /// <summary>
    /// Accepts a configured terminal as either an application name or a path to a bundle, because
    /// dragging the bundle into the settings field is the obvious thing to do and produces a path.
    /// Anything that is not recognisably iTerm is driven the way Terminal is, which is the idiom
    /// every AppleScript-aware terminal supports.
    /// </summary>
    private static Result<TerminalProgram> Configured(string configured)
    {
        var name = configured.EndsWith(ApplicationBundleExtension, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(configured.TrimEnd(Path.DirectorySeparatorChar))
            : configured;

        return name.Equals(ITermApplicationName, StringComparison.OrdinalIgnoreCase)
            ? Found(name, TerminalKind.MacITerm)
            : Found(name, TerminalKind.MacTerminalApp);
    }

    private static Result<TerminalProgram> Found(string applicationName, TerminalKind kind) =>
        Result<TerminalProgram>.Success(new TerminalProgram
        {
            Executable = applicationName,
            Kind = kind,
        });
}
