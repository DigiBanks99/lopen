using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Llm;

/// <summary>
/// Extension methods for registering LLM module services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Lopen LLM module services.
    /// </summary>
    public static IServiceCollection AddLopenLlm(this IServiceCollection services)
    {
        services.AddSingleton<ILlmService, StubLlmService>();
        services.AddSingleton<IModelSelector, DefaultModelSelector>();
        services.AddSingleton<ITokenTracker, InMemoryTokenTracker>();

        return services;
    }
}
