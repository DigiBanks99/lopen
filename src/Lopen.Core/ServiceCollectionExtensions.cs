using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Core;

/// <summary>
/// Extension methods for registering Core module services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core module services with the dependency injection container.
    /// </summary>
    public static IServiceCollection AddLopenCore(this IServiceCollection services)
    {
        services.AddSingleton<IGitService, GitCliService>();
        services.AddSingleton<ISpecificationParser, MarkdigSpecificationParser>();
        services.AddSingleton<IContentHasher, XxHashContentHasher>();
        services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();

        return services;
    }
}
