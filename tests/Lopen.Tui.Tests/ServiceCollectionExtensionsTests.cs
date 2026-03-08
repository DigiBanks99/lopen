using Lopen.Core;
using Microsoft.Extensions.DependencyInjection;
using Lopen.Tui;

namespace Lopen.Tui.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenTui_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IServiceCollection result = services.AddLopenTui();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenTui_RegistersIOutputRenderer()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IOutputRenderer));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void AddLopenTui_RegistersIUserPromptQueue()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IUserPromptQueue));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void AddLopenTui_RegistersTuiUserPromptQueue()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TuiUserPromptQueue));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddLopenTui_RegistersLopenLineEditor()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(LopenLineEditor));
        Assert.NotNull(descriptor);
    }
}
