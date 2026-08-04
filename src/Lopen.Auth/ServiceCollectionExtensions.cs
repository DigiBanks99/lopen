using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Auth;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Lopen authentication module services.
    /// </summary>
    public static IServiceCollection AddLopenAuth(this IServiceCollection services)
    {
        services.AddSingleton<ITokenSourceResolver, EnvironmentTokenSourceResolver>();
        services.AddSingleton<IGhCliAdapter, GhCliAdapter>();
        services.AddSingleton<CopilotAuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<CopilotAuthService>());
        services.AddSingleton<IAuthTokenProvider>(sp => sp.GetRequiredService<CopilotAuthService>());

        return services;
    }
}
