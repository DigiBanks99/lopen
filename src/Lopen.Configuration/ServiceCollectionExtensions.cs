using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Lopen configuration module services.
    /// Uses the provided <see cref="LopenOptions"/> instance or discovers and builds one.
    /// </summary>
    public static IServiceCollection AddLopenConfiguration(
        this IServiceCollection services,
        LopenOptions? options = null)
    {
        IConfigurationRoot? configRoot = null;

        if (options is null)
        {
            var globalPath = LopenConfigurationBuilder.GetDefaultGlobalConfigPath();
            var projectPath = LopenConfigurationBuilder.DiscoverProjectConfigPath(Directory.GetCurrentDirectory());
            var result = new LopenConfigurationBuilder(globalPath, projectPath).Build();
            options = result.Options;
            configRoot = result.Configuration;
        }

        services.AddSingleton(options);
        services.AddSingleton(options.Models);
        services.AddSingleton(options.Budget);
        services.AddSingleton(options.Oracle);
        services.AddSingleton(options.Workflow);
        services.AddSingleton(options.Session);
        services.AddSingleton(options.Git);
        services.AddSingleton(options.ToolDiscipline);
        services.AddSingleton(options.Display);

        if (configRoot is not null)
            services.AddSingleton<IConfigurationRoot>(configRoot);

        services.AddSingleton<IBudgetEnforcer, BudgetEnforcer>();

        return services;
    }
}
