namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// The environment variables a launched program must not inherit from whatever process happened to
/// start this application.
/// </summary>
/// <remarks>
/// <para>
/// A launcher is started from wherever its user starts it, and one of those places is a shell
/// running inside an editor's extension host. That shell carries <c>ELECTRON_RUN_AS_NODE=1</c>, and
/// an Electron executable started with it inherited is not that application at all: it runs as a
/// plain Node interpreter and treats the folder it was given as a module to require. Measured on
/// this machine, <c>Code.exe &lt;folder&gt;</c> under that variable exits with code 1 and
/// <c>Cannot find module</c>, while starting the process itself succeeds — which is exactly the
/// shape of a failure nobody sees.
/// </para>
/// <para>
/// The list is one variable long because it was measured rather than guessed. The neighbours that
/// look like they belong on it do not: with <c>ELECTRON_NO_ATTACH_CONSOLE</c>, with
/// <c>ELECTRON_NO_ASAR</c> and with <c>NODE_OPTIONS</c> set, the same command opened the editor and
/// exited zero. Only the one that changes which program the executable <em>is</em> breaks it, and
/// only that one is removed.
/// </para>
/// </remarks>
public static class InheritedEnvironment
{
    private const string RunElectronAsNode = "ELECTRON_RUN_AS_NODE";

    /// <summary>The variables cleared from a child's environment before it is started.</summary>
    public static IReadOnlyList<string> ClearedVariables { get; } = [RunElectronAsNode];

    /// <summary>
    /// Clears from a child's inherited environment every variable that would change what the
    /// program being started is.
    /// </summary>
    /// <param name="environment">The child's environment, as inherited from this process.</param>
    public static void Scrub(IDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var variable in ClearedVariables)
        {
            environment.Remove(variable);
        }
    }
}
