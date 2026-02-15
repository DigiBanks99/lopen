using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lopen.Tui.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenTui_RegistersWithoutError()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Record.Exception(() => services.AddLopenTui());

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenTui_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var result = services.AddLopenTui();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenTui_ServiceProviderBuildsWithoutError()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenTui_ResolvesTuiApplication()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var app = provider.GetService<ITuiApplication>();

        Assert.NotNull(app);
    }

    [Fact]
    public void AddLopenTui_ResolvesComponentGallery()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var gallery = provider.GetService<IComponentGallery>();

        Assert.NotNull(gallery);
    }

    [Fact]
    public void AddLopenTui_TuiApplication_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var app1 = provider.GetRequiredService<ITuiApplication>();
        var app2 = provider.GetRequiredService<ITuiApplication>();

        Assert.Same(app1, app2);
    }

    [Fact]
    public void AddLopenTui_ComponentGallery_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var gallery1 = provider.GetRequiredService<IComponentGallery>();
        var gallery2 = provider.GetRequiredService<IComponentGallery>();

        Assert.Same(gallery1, gallery2);
    }
}
