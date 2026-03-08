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

        using ServiceProvider provider = services.BuildServiceProvider();
        ITokenSourceResolver? resolver = provider.GetService<ITokenSourceResolver>();

        Assert.NotNull(resolver);
        Assert.IsType<EnvironmentTokenSourceResolver>(resolver);
    }

    [Fact]
    public void AddLopenAuth_RegistersIAuthService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using ServiceProvider provider = services.BuildServiceProvider();
        IAuthService? authService = provider.GetService<IAuthService>();

        Assert.NotNull(authService);
        Assert.IsType<CopilotAuthService>(authService);
    }

    [Fact]
    public void AddLopenAuth_RegistersIGhCliAdapter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using ServiceProvider provider = services.BuildServiceProvider();
        IGhCliAdapter? adapter = provider.GetService<IGhCliAdapter>();

        Assert.NotNull(adapter);
        Assert.IsType<GhCliAdapter>(adapter);
    }

    [Fact]
    public void AddLopenAuth_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddLopenAuth();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenAuth_ITokenSourceResolver_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITokenSourceResolver first = provider.GetRequiredService<ITokenSourceResolver>();
        ITokenSourceResolver second = provider.GetRequiredService<ITokenSourceResolver>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddLopenAuth_IAuthService_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenAuth();

        using ServiceProvider provider = services.BuildServiceProvider();
        IAuthService first = provider.GetRequiredService<IAuthService>();
        IAuthService second = provider.GetRequiredService<IAuthService>();

        Assert.Same(first, second);
    }
}
