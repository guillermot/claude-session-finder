using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SessionFinder.Presentation.Tests;

public sealed class PresentationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSessionFinderPresentation_ValidArguments_ReturnsTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var returned = services.AddSessionFinderPresentation(configuration);

        returned.Should().BeSameAs(services);
    }
}
