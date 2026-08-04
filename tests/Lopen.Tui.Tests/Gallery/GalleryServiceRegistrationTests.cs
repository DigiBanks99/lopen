using Lopen.Tui.Gallery;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Lopen.Tui.Tests.Gallery;

public class GalleryServiceRegistrationTests
{
    [Fact]
    public void AddLopenTui_RegistersGalleryComponents()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IGalleryComponent> components = provider.GetServices<IGalleryComponent>();

        Assert.NotNull(components);
        List<IGalleryComponent> componentList = components.ToList();
        Assert.True(componentList.Count >= 7, $"Expected at least 7 gallery components, got {componentList.Count}");
    }

    [Fact]
    public void AddLopenTui_RegistersAllExpectedComponentTypes()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        ServiceProvider provider = services.BuildServiceProvider();
        List<IGalleryComponent> components = provider.GetServices<IGalleryComponent>().ToList();

        HashSet<string> names = new(components.Select(c => c.DisplayName));
        Assert.Contains("Workflow Overview (4 states)", names);
        Assert.Contains("Prompt Input", names);
        Assert.Contains("Command Palette", names);
        Assert.Contains("Response Rendering", names);
        Assert.Contains("Session List", names);
        Assert.Contains("Error Panel", names);
        Assert.Contains("Slash Command Help", names);
    }

    [Fact]
    public void AddLopenTui_RegistersGalleryComponentsAsSingletons()
    {
        ServiceCollection services = new();
        services.AddLopenTui();

        List<ServiceDescriptor> galleryDescriptors = services
            .Where(d => d.ServiceType == typeof(IGalleryComponent))
            .ToList();

        Assert.True(galleryDescriptors.Count >= 7,
            $"Expected at least 7 IGalleryComponent descriptors, got {galleryDescriptors.Count}");

        foreach (ServiceDescriptor descriptor in galleryDescriptors)
        {
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }
    }

    [Fact]
    public void AddLopenTui_ResolvedComponents_CanConstructGallery()
    {
        ServiceCollection services = new();
        services.AddLopenTui();
        ServiceProvider provider = services.BuildServiceProvider();

        IAnsiConsole console = provider.GetRequiredService<IAnsiConsole>();
        IEnumerable<IGalleryComponent> components = provider.GetServices<IGalleryComponent>();

        ComponentGallery gallery = new(console, components);

        Assert.True(gallery.ComponentNames.Count >= 7,
            $"Expected at least 7 component names, got {gallery.ComponentNames.Count}");
    }
}
