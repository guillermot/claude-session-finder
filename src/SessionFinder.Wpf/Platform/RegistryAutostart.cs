using System.IO;
using System.Security;
using Microsoft.Win32;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Starts the application at sign-in through the per-user <c>Run</c> key.
/// </summary>
/// <remarks>
/// <para>
/// The per-user key is used rather than the machine-wide one or a scheduled task: it needs no
/// elevation, it is removed with the user's profile, and it is the place a user looking for
/// start-up entries expects to find one. A tray tool has no business writing anywhere else.
/// </para>
/// <para>
/// The path comes from <see cref="Environment.ProcessPath"/> rather than from the assembly. A
/// single-file application is extracted before it runs, so the assembly's location is either empty
/// or a temporary directory that will not exist at the next sign-in — writing it would produce a
/// start-up entry that silently fails forever.
/// </para>
/// <para>
/// A registration pointing at some other copy of this application counts as not registered, so that
/// moving the executable is repaired by the next start rather than leaving a dead entry behind and
/// a second one beside it.
/// </para>
/// </remarks>
internal sealed class RegistryAutostart : IAutostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClaudeSessionFinder";
    private const char Quote = '"';
    private const string NoProcessPathReason =
        "the running executable could not be located, which should not be possible for a published build";

    /// <inheritdoc />
    public Result<bool> ReadIsEnabled()
    {
        if (ResolveCommand() is not { } expected)
        {
            return Result<bool>.Failure(AppError.AutostartUnavailable(NoProcessPathReason));
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var stored = key?.GetValue(ValueName) as string;

            return Result<bool>.Success(PointsAtThisApplication(stored, expected));
        }
        catch (Exception exception) when (IsRegistryFailure(exception))
        {
            return Result<bool>.Failure(AppError.AutostartUnavailable(exception.Message));
        }
    }

    /// <inheritdoc />
    public Result Apply(bool shouldStartWithSession)
    {
        if (ResolveCommand() is not { } command)
        {
            return Result.Failure(AppError.AutostartUnavailable(NoProcessPathReason));
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (key is null)
            {
                return Result.Failure(AppError.AutostartUnavailable("the start-up key could not be opened"));
            }

            Write(key, command, shouldStartWithSession);

            return Result.Success();
        }
        catch (Exception exception) when (IsRegistryFailure(exception))
        {
            return Result.Failure(AppError.AutostartUnavailable(exception.Message));
        }
    }

    private static void Write(RegistryKey key, string command, bool shouldStartWithSession)
    {
        if (shouldStartWithSession)
        {
            key.SetValue(ValueName, command, RegistryValueKind.String);
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Builds the command line the operating system will run, quoted because the path to a program
    /// installed under a user's profile or under Program Files contains spaces.
    /// </summary>
    private static string? ResolveCommand() =>
        Environment.ProcessPath is { Length: > 0 } path ? Quote + path + Quote : null;

    private static bool PointsAtThisApplication(string? stored, string expected) =>
        string.Equals(stored?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsRegistryFailure(Exception exception) =>
        exception is SecurityException or UnauthorizedAccessException or IOException;
}
