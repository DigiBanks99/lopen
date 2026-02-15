using Microsoft.Extensions.Logging;

namespace Lopen.Auth;

/// <summary>
/// Stub implementation of <see cref="IAuthService"/> for use before the Copilot SDK is integrated.
/// Resolves environment variable tokens; reports SDK credentials as unavailable.
/// </summary>
internal sealed class StubAuthService : IAuthService
{
    private readonly ITokenSourceResolver _tokenSourceResolver;
    private readonly ILogger<StubAuthService> _logger;

    public StubAuthService(ITokenSourceResolver tokenSourceResolver, ILogger<StubAuthService> logger)
    {
        _tokenSourceResolver = tokenSourceResolver ?? throw new ArgumentNullException(nameof(tokenSourceResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task LoginAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Interactive login requires the Copilot SDK. Set GH_TOKEN or GITHUB_TOKEN for now.");
        throw new AuthenticationException(
            "Interactive login is not yet available. Set the GH_TOKEN environment variable to authenticate.");
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logout requested — no SDK credentials to clear.");
        return Task.CompletedTask;
    }

    public Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = _tokenSourceResolver.Resolve();

        return Task.FromResult(result.Source switch
        {
            AuthCredentialSource.GhToken => new AuthStatusResult(
                AuthState.Authenticated, AuthCredentialSource.GhToken),
            AuthCredentialSource.GitHubToken => new AuthStatusResult(
                AuthState.Authenticated, AuthCredentialSource.GitHubToken),
            _ => new AuthStatusResult(
                AuthState.NotAuthenticated, AuthCredentialSource.None,
                ErrorMessage: "Not authenticated. Run 'lopen auth login' or set GH_TOKEN."),
        });
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);

        if (status.State != AuthState.Authenticated)
        {
            throw new AuthenticationException(
                "Authentication failed. No valid credentials found. "
                + "Run 'lopen auth login' or set the GH_TOKEN environment variable.");
        }
    }
}
