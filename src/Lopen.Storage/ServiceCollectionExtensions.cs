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
    /// <param name="services">The service collection.</param>
    /// <param name="projectRoot">
    /// The project root directory for session storage. When provided, also registers
    /// <see cref="ISessionManager"/> and <see cref="IAutoSaveService"/>.
    /// </param>
    public static IServiceCollection AddLopenStorage(
        this IServiceCollection services,
        string? projectRoot = null)
    {
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();

        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            services.AddSingleton<ISessionManager>(sp =>
                new SessionManager(
                    sp.GetRequiredService<IFileSystem>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SessionManager>>(),
                    projectRoot));
            services.AddSingleton<IAutoSaveService, AutoSaveService>();
            services.AddSingleton<IPlanManager>(sp =>
                new PlanManager(
                    sp.GetRequiredService<IFileSystem>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PlanManager>>(),
                    projectRoot));
            services.AddSingleton<ISectionCache>(sp =>
                new SectionCache(
                    sp.GetRequiredService<IFileSystem>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SectionCache>>(),
                    projectRoot));
            services.AddSingleton<IAssessmentCache>(sp =>
                new AssessmentCache(
                    sp.GetRequiredService<IFileSystem>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AssessmentCache>>(),
                    projectRoot));
        }

        return services;
    }
}
