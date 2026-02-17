using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Auth.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenAuth_RegistersITokenSourceResolver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetService<ITokenSourceResolver>();

        Assert.NotNull(resolver);
        Assert.IsType<EnvironmentTokenSourceResolver>(resolver);
    }

    [Fact]
    public void AddLopenAuth_RegistersIAuthService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using var provider = services.BuildServiceProvider();
        var authService = provider.GetService<IAuthService>();

        Assert.NotNull(authService);
        Assert.IsType<CopilotAuthService>(authService);
    }

    [Fact]
    public void AddLopenAuth_RegistersIGhCliAdapter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using var provider = services.BuildServiceProvider();
        var adapter = provider.GetService<IGhCliAdapter>();

        Assert.NotNull(adapter);
        Assert.IsType<GhCliAdapter>(adapter);
    }

    [Fact]
    public void AddLopenAuth_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenAuth();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenAuth_ITokenSourceResolver_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITokenSourceResolver>();
        var second = provider.GetRequiredService<ITokenSourceResolver>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddLopenAuth_IAuthService_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IAuthService>();
        var second = provider.GetRequiredService<IAuthService>();

        Assert.Same(first, second);
    }
}
