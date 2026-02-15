namespace Lopen.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenCore_ReturnsServiceCollection()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        var result = services.AddLopenCore();

        Assert.Same(services, result);
    }
}
