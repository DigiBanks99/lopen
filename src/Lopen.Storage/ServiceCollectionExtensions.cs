using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Storage;

/// <summary>
/// Extension methods for registering storage module services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Lopen storage module services.
    /// </summary>
    public static IServiceCollection AddLopenStorage(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();

        return services;
    }
}
