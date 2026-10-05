using SessionFinder.Infrastructure.Recap;

namespace SessionFinder.Infrastructure.Tests.Recap;

public sealed class ClaudeExecutableLocatorTests
{
    private const string Home = "/Users/me";

    [Fact]
    public void Find_OnThePath_PrefersThePath()
    {
        var found = Find("/usr/bin:/opt/tools", "/opt/tools/claude", "/Users/me/.local/bin/claude");

        found.Should().Be(Path.Combine("/opt/tools", "claude"));
    }

    [Fact]
    public void Find_NotOnAMenuBarLaunchersMinimalPath_FindsTheNativeInstall()
    {
        var found = Find("/usr/bin:/bin", Path.Combine(Home, ".local", "bin", "claude"));

        found.Should().Be(Path.Combine(Home, ".local", "bin", "claude"));
    }

    [Fact]
    public void Find_AConfiguredPathThatExists_WinsOverEverything()
    {
        var found = ClaudeExecutableLocator.Find("/custom/claude", "/opt/tools", Home, "/appdata", isWindows: false, path => true);

        found.Should().Be("/custom/claude");
    }

    [Fact]
    public void Find_AConfiguredPathThatDoesNotExist_IsReportedAsMissingRatherThanReplaced()
    {
        var found = ClaudeExecutableLocator.Find("/custom/claude", "/opt/tools", Home, "/appdata", isWindows: false, path => path != "/custom/claude");

        found.Should().BeNull();
    }

    [Fact]
    public void Find_Nowhere_ReturnsNull()
    {
        Find("/usr/bin").Should().BeNull();
    }

    private static string? Find(string pathVariable, params string[] existing) =>
        ClaudeExecutableLocator.Find(null, pathVariable, Home, "/appdata", isWindows: false, existing.Contains);
}
