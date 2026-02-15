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
        services.AddSingleton<IAuthService, StubAuthService>();

        return services;
    }
}
