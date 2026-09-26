using System.Diagnostics;
using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Core.Tests.Features.SessionActions;

public sealed class InheritedEnvironmentTests
{
    private const string RunElectronAsNode = "ELECTRON_RUN_AS_NODE";

    [Fact]
    public void Scrub_AnEnvironmentInheritedFromAnExtensionHost_ClearsTheVariableThatTurnsTheEditorIntoAnInterpreter()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [RunElectronAsNode] = "1",
        };

        InheritedEnvironment.Scrub(environment);

        environment.Should().NotContainKey(RunElectronAsNode);
    }

    [Fact]
    public void Scrub_AnEnvironmentWithoutIt_LeavesEverythingElseWhereItWas()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = @"C:\Windows",
            ["VSCODE_PID"] = "3356",
            [RunElectronAsNode] = "1",
        };

        InheritedEnvironment.Scrub(environment);

        environment.Should().ContainKey("PATH").And.ContainKey("VSCODE_PID");
    }

    [Fact]
    public void Scrub_AnEnvironmentThatNeverHadIt_ChangesNothing()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = @"C:\Windows",
        };

        InheritedEnvironment.Scrub(environment);

        environment.Should().HaveCount(1);
    }

    [Fact]
    public void Scrub_TheEnvironmentOfARealProcessDescription_ClearsWhatTheChildWouldHaveInherited()
    {
        Environment.SetEnvironmentVariable(RunElectronAsNode, "1");

        try
        {
            var startInfo = new ProcessStartInfo { UseShellExecute = false };

            InheritedEnvironment.Scrub(startInfo.Environment);

            startInfo.Environment.Should().NotContainKey(RunElectronAsNode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RunElectronAsNode, null);
        }
    }

    [Fact]
    public void ClearedVariables_IsLimitedToWhatWasMeasuredToBreakALaunch()
    {
        InheritedEnvironment.ClearedVariables.Should().Equal(RunElectronAsNode);
    }
}
