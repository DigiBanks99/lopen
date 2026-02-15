using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        // Auth token provider — default is null (SDK resolves credentials).
        // Consumers can register their own IGitHubTokenProvider before calling this.
        services.TryAddSingleton<IGitHubTokenProvider, NullGitHubTokenProvider>();
        services.TryAddSingleton<ICopilotClientProvider, CopilotClientProvider>();
        services.AddSingleton<ILlmService, CopilotLlmService>();
        services.AddSingleton<IModelSelector, DefaultModelSelector>();
        services.AddSingleton<ITokenTracker, InMemoryTokenTracker>();
        services.AddSingleton<IToolRegistry, DefaultToolRegistry>();
        services.AddSingleton<IPromptBuilder, DefaultPromptBuilder>();
        services.AddSingleton<IVerificationTracker, VerificationTracker>();
        services.AddSingleton<IOracleVerifier, OracleVerifier>();
        services.AddSingleton<IContextBudgetManager, ContextBudgetManager>();
        services.AddSingleton<ITaskStatusGate, TaskStatusGate>();

        return services;
    }
}
