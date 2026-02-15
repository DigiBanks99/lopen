using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenCore_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenCore();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenCore_RegistersGitService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IGitService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersSpecificationParser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ISpecificationParser>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersContentHasher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IContentHasher>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersGuardrailPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IGuardrailPipeline>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var git1 = provider.GetService<IGitService>();
        var git2 = provider.GetService<IGitService>();

        Assert.Same(git1, git2);
    }
}
