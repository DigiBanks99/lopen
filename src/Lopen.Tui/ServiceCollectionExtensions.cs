using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Tui;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers TUI services including the application lifecycle and component gallery.
    /// </summary>
    public static IServiceCollection AddLopenTui(this IServiceCollection services)
    {
        services.AddSingleton<ITuiApplication, StubTuiApplication>();
        services.AddSingleton<IComponentGallery, ComponentGallery>();
        return services;
    }
}
