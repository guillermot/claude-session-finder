using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SessionFinder.Core.Tests;

public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSessionFinderCore_ValidArguments_ReturnsTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var returned = services.AddSessionFinderCore(configuration);

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSessionFinderCore_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        var register = () => services.AddSessionFinderCore(configuration: null!);

        register.Should().Throw<ArgumentNullException>();
    }
}
