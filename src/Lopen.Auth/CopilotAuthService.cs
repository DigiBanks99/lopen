using Microsoft.Extensions.Logging;

namespace Lopen.Auth;

/// <summary>
/// Production auth service that combines environment variable resolution with
/// gh CLI delegation for device flow, status checks, and logout.
/// </summary>
internal sealed class CopilotAuthService : IAuthService
{
    private readonly ITokenSourceResolver _tokenSourceResolver;
    private readonly IGhCliAdapter _ghCli;
    private readonly ILogger<CopilotAuthService> _logger;
    private readonly Func<bool> _isInteractive;

    public CopilotAuthService(
        ITokenSourceResolver tokenSourceResolver,
        IGhCliAdapter ghCli,
        ILogger<CopilotAuthService> logger)
        : this(tokenSourceResolver, ghCli, logger, () => Environment.UserInteractive && !Console.IsInputRedirected)
    {
    }

    /// <summary>
    /// Constructor accepting a custom interactivity check for testability.
    /// </summary>
    internal CopilotAuthService(
        ITokenSourceResolver tokenSourceResolver,
        IGhCliAdapter ghCli,
        ILogger<CopilotAuthService> logger,
        Func<bool> isInteractive)
    {
        _tokenSourceResolver = tokenSourceResolver ?? throw new ArgumentNullException(nameof(tokenSourceResolver));
        _ghCli = ghCli ?? throw new ArgumentNullException(nameof(ghCli));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isInteractive = isInteractive ?? throw new ArgumentNullException(nameof(isInteractive));
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        // JOB-015: Block interactive login in non-interactive/headless environments
        if (!_isInteractive())
        {
            throw new AuthenticationException(AuthErrorMessages.HeadlessLoginNotSupported);
        }

        // JOB-012: Check gh CLI availability
        if (!await _ghCli.IsAvailableAsync(cancellationToken))
        {
            throw new AuthenticationException(AuthErrorMessages.GhCliNotFound);
        }

        // JOB-012: Delegate device flow to gh CLI
        var username = await _ghCli.LoginAsync(cancellationToken);
        _logger.LogInformation("Authenticated as {Username} via device flow", username);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        // JOB-014: Clear gh CLI credentials
        await _ghCli.LogoutAsync(cancellationToken);
        _logger.LogInformation("Logged out of gh CLI credentials");

        // JOB-014: Warn if environment variables are still set
        var envResult = _tokenSourceResolver.Resolve();
        if (envResult.Source is AuthCredentialSource.GhToken)
        {
            _logger.LogWarning("{Warning}", AuthErrorMessages.EnvVarStillSet("GH_TOKEN"));
        }
        else if (envResult.Source is AuthCredentialSource.GitHubToken)
        {
            _logger.LogWarning("{Warning}", AuthErrorMessages.EnvVarStillSet("GITHUB_TOKEN"));
        }
    }

    public async Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        // JOB-016: Check environment variables first (highest precedence)
        var envResult = _tokenSourceResolver.Resolve();

        if (envResult.Source is AuthCredentialSource.GhToken)
        {
            return new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GhToken);
        }

        if (envResult.Source is AuthCredentialSource.GitHubToken)
        {
            return new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GitHubToken);
        }

        // JOB-013: Fall back to gh CLI stored credentials
        var ghStatus = await _ghCli.GetStatusAsync(cancellationToken);

        if (ghStatus is not null)
        {
            // Validate that stored credentials are actually functional
            var isValid = await _ghCli.ValidateCredentialsAsync(cancellationToken);
            if (!isValid)
            {
                return new AuthStatusResult(
                    AuthState.InvalidCredentials,
                    AuthCredentialSource.SdkCredentials,
                    Username: ghStatus.Username,
                    ErrorMessage: AuthErrorMessages.InvalidCredentials);
            }

            return new AuthStatusResult(
                AuthState.Authenticated,
                AuthCredentialSource.SdkCredentials,
                Username: ghStatus.Username);
        }

        // No credentials found
        return new AuthStatusResult(
            AuthState.NotAuthenticated,
            AuthCredentialSource.None,
            ErrorMessage: AuthErrorMessages.NotAuthenticated);
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);

        switch (status.State)
        {
            case AuthState.Authenticated:
                _logger.LogDebug("Pre-flight auth check passed: {Source}", status.Source);
                return;

            case AuthState.InvalidCredentials:
                throw new AuthenticationException(AuthErrorMessages.InvalidCredentials);

            case AuthState.NotAuthenticated:
            default:
                throw new AuthenticationException(AuthErrorMessages.PreFlightFailed);
        }
    }
}
