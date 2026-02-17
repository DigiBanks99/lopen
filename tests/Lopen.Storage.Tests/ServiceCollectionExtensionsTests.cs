using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Storage.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenStorage_RegistersIFileSystem()
    {
        var services = new ServiceCollection();
        services.AddLopenStorage();

        using var provider = services.BuildServiceProvider();
        var fileSystem = provider.GetService<IFileSystem>();

        Assert.NotNull(fileSystem);
        Assert.IsType<PhysicalFileSystem>(fileSystem);
    }

    [Fact]
    public void AddLopenStorage_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenStorage();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenStorage_IFileSystem_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLopenStorage();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IFileSystem>();
        var second = provider.GetRequiredService<IFileSystem>();

        Assert.Same(first, second);
    }
}
