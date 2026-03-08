using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Tui;

/// <summary>
/// Extension methods for registering TUI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Lopen TUI services to the service collection.
    /// </summary>
    public static IServiceCollection AddLopenTui(this IServiceCollection services)
    {
        return services;
    }
}
