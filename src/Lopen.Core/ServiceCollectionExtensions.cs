using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
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
    /// <param name="services">The service collection.</param>
    /// <param name="projectRoot">The project root directory for git and module scanning.</param>
    public static IServiceCollection AddLopenCore(this IServiceCollection services, string? projectRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            services.AddSingleton<IGitService>(sp =>
                new GitCliService(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GitCliService>>(),
                    projectRoot));
            services.AddSingleton<IGitWorkflowService, GitWorkflowService>();
            services.AddSingleton<IRevertService, RevertService>();
            services.AddSingleton<IModuleScanner>(sp =>
                new ModuleScanner(
                    sp.GetRequiredService<Lopen.Storage.IFileSystem>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModuleScanner>>(),
                    projectRoot));
            services.AddSingleton<IModuleLister, ModuleLister>();
        }

        services.AddSingleton<ISpecificationParser, MarkdigSpecificationParser>();
        services.AddSingleton<IContentHasher, XxHashContentHasher>();
        services.AddSingleton<IDriftDetector, DriftDetector>();
        services.AddSingleton<ISectionExtractor, SectionExtractor>();
        services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();

        return services;
    }
}
