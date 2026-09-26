using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Tests.FileSystem;

/// <summary>
/// The trap these guard against is not an exception but a plausible wrong answer:
/// <see cref="Environment.SpecialFolder"/> resolves happily on macOS and resolves to the XDG-style
/// folders .NET uses on Linux, so only the exact location is evidence.
/// </summary>
/// <remarks>
/// The macOS expectations are written as literal paths because macOS is the platform these run on.
/// The Windows ones are written as segments instead: <see cref="Path.Combine(string, string)"/>
/// joins with the running platform's separator, so a literal backslash path would be asserting the
/// test host rather than the layout. Which root, which folder names, and in which order is the
/// whole of the claim either way.
/// </remarks>
public sealed class ApplicationPathsTests
{
    private const string MacHome = "/Users/someone";
    private const string WindowsHome = @"C:\Users\someone";
    private const string RoamingAppData = @"C:\Users\someone\AppData\Roaming";
    private const string LocalAppData = @"C:\Users\someone\AppData\Local";

    [Fact]
    public void ResolveSettingsFile_MacOs_PutsItUnderApplicationSupport()
    {
        ApplicationPaths.ResolveSettingsFile(true, MacHome, RoamingAppData)
            .Should().Be("/Users/someone/Library/Application Support/ClaudeSessionFinder/settings.json");
    }

    [Fact]
    public void ResolveSettingsFile_Windows_PutsItInTheRoamingProfileSoItFollowsThePerson()
    {
        Segments(ApplicationPaths.ResolveSettingsFile(false, WindowsHome, RoamingAppData))
            .Should().Equal(
                "C:", "Users", "someone", "AppData", "Roaming",
                "ClaudeSessionFinder", "settings.json");
    }

    [Fact]
    public void ResolveLogFolder_MacOs_PutsItWhereThatSystemKeepsLogs()
    {
        ApplicationPaths.ResolveLogFolder(true, MacHome, LocalAppData)
            .Should().Be("/Users/someone/Library/Logs/ClaudeSessionFinder");
    }

    [Fact]
    public void ResolveLogFolder_Windows_KeepsItOutOfTheRoamingProfile()
    {
        Segments(ApplicationPaths.ResolveLogFolder(false, WindowsHome, LocalAppData))
            .Should().Equal(
                "C:", "Users", "someone", "AppData", "Local", "ClaudeSessionFinder", "logs");
    }

    [Fact]
    public void ResolveIndexPath_MacOs_SitsBesideTheSettingsRatherThanInAFolderOfItsOwn()
    {
        ApplicationPaths.ResolveIndexPath(null, true, MacHome, LocalAppData)
            .Should().Be("/Users/someone/Library/Application Support/ClaudeSessionFinder/index.db");
    }

    [Fact]
    public void ResolveIndexPath_Windows_StaysInTheLocalProfileBecauseItIsARebuildableCache()
    {
        Segments(ApplicationPaths.ResolveIndexPath(null, false, WindowsHome, LocalAppData))
            .Should().Equal(
                "C:", "Users", "someone", "AppData", "Local", "ClaudeSessionFinder", "index.db");
    }

    [Fact]
    public void ResolveIndexPath_AConfiguredPath_WinsOnEitherPlatform()
    {
        var configured = Path.Combine(Path.GetTempPath(), "elsewhere", "index.db");

        ApplicationPaths.ResolveIndexPath(configured, true, MacHome, LocalAppData)
            .Should().Be(Path.GetFullPath(configured));
    }

    [Fact]
    public void ResolveIndexPath_ABlankConfiguredPath_FallsBackToTheDefaultLocation()
    {
        ApplicationPaths.ResolveIndexPath("   ", true, MacHome, LocalAppData)
            .Should().Be("/Users/someone/Library/Application Support/ClaudeSessionFinder/index.db");
    }

    private static string[] Segments(string path) =>
        path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
}
