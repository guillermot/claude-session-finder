using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Infrastructure.Tests.FileSystem;

public sealed class ClaudeConfigurationDirectoryTests
{
    private const string UserProfile = @"C:\Users\someone";
    private const string ConfiguredDirectory = @"D:\claude-config";
    private const string EnvironmentDirectory = @"E:\claude-from-environment";

    [Fact]
    public void Resolve_ExplicitlyConfiguredDirectory_WinsOverTheEnvironment()
    {
        var resolved = ClaudeConfigurationDirectory.Resolve(ConfiguredDirectory, EnvironmentDirectory, UserProfile);

        resolved.Should().Be(Path.GetFullPath(ConfiguredDirectory));
    }

    [Fact]
    public void Resolve_NothingConfigured_UsesTheEnvironmentVariable()
    {
        var resolved = ClaudeConfigurationDirectory.Resolve(null, EnvironmentDirectory, UserProfile);

        resolved.Should().Be(Path.GetFullPath(EnvironmentDirectory));
    }

    [Fact]
    public void Resolve_BlankConfiguredDirectory_IsTreatedAsNotConfigured()
    {
        var resolved = ClaudeConfigurationDirectory.Resolve("   ", EnvironmentDirectory, UserProfile);

        resolved.Should().Be(Path.GetFullPath(EnvironmentDirectory));
    }

    [Fact]
    public void Resolve_NeitherConfiguredNorInTheEnvironment_UsesTheUserProfile()
    {
        var resolved = ClaudeConfigurationDirectory.Resolve(null, null, UserProfile);

        resolved.Should().Be(Path.Combine(UserProfile, ClaudeConfigurationDirectory.DefaultFolderName));
    }

    [Fact]
    public void Resolve_BlankEnvironmentVariable_IsTreatedAsNotSet()
    {
        var resolved = ClaudeConfigurationDirectory.Resolve(null, string.Empty, UserProfile);

        resolved.Should().Be(Path.Combine(UserProfile, ClaudeConfigurationDirectory.DefaultFolderName));
    }
}
