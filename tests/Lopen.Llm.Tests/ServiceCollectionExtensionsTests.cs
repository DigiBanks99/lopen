using Lopen.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lopen.Llm.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new LopenOptions()));
        services.AddLopenLlm();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddLopenLlm_RegistersILlmService()
    {
        using var provider = BuildProvider();

        var service = provider.GetService<ILlmService>();

        Assert.NotNull(service);
        Assert.IsType<StubLlmService>(service);
    }

    [Fact]
    public void AddLopenLlm_RegistersIModelSelector()
    {
        using var provider = BuildProvider();

        var selector = provider.GetService<IModelSelector>();

        Assert.NotNull(selector);
        Assert.IsType<DefaultModelSelector>(selector);
    }

    [Fact]
    public void AddLopenLlm_RegistersITokenTracker()
    {
        using var provider = BuildProvider();

        var tracker = provider.GetService<ITokenTracker>();

        Assert.NotNull(tracker);
        Assert.IsType<InMemoryTokenTracker>(tracker);
    }

    [Fact]
    public void AddLopenLlm_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenLlm();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenLlm_ILlmService_IsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ILlmService>();
        var second = provider.GetRequiredService<ILlmService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddLopenLlm_IModelSelector_IsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IModelSelector>();
        var second = provider.GetRequiredService<IModelSelector>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddLopenLlm_ITokenTracker_IsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ITokenTracker>();
        var second = provider.GetRequiredService<ITokenTracker>();

        Assert.Same(first, second);
    }
}
