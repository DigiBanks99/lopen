namespace Lopen.Core;

/// <summary>
/// Result of an authentication status check.
/// </summary>
public record AuthStatus(bool IsAuthenticated, string? Username = null, string? Source = null);

/// <summary>
/// Service for managing authentication with GitHub.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gets the current access token.
    /// </summary>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Checks the current authentication status.
    /// </summary>
    Task<AuthStatus> GetStatusAsync();

    /// <summary>
    /// Stores a token.
    /// </summary>
    Task StoreTokenAsync(string token);

    /// <summary>
    /// Stores token info with refresh token and expiry.
    /// </summary>
    Task StoreTokenInfoAsync(TokenInfo tokenInfo);

    /// <summary>
    /// Clears stored credentials.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Authentication service supporting environment variable and file-based storage.
/// Supports automatic token refresh when tokens are close to expiry.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ICredentialStore _credentialStore;
    private readonly ITokenInfoStore? _tokenInfoStore;
    private readonly IDeviceFlowAuth? _deviceFlowAuth;
    private const string GitHubTokenEnvVar = "GITHUB_TOKEN";

    /// <summary>
    /// Buffer time before expiry to trigger refresh (5 minutes).
    /// </summary>
    public static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(5);

    public AuthService(ICredentialStore credentialStore)
        : this(credentialStore, null, null)
    {
    }

    public AuthService(ICredentialStore credentialStore, ITokenInfoStore? tokenInfoStore)
        : this(credentialStore, tokenInfoStore, null)
    {
    }

    public AuthService(ICredentialStore credentialStore, ITokenInfoStore? tokenInfoStore, IDeviceFlowAuth? deviceFlowAuth)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _tokenInfoStore = tokenInfoStore;
        _deviceFlowAuth = deviceFlowAuth;
    }

    public async Task<string?> GetTokenAsync()
    {
        // Priority 1: Environment variable (externally managed, no refresh)
        var envToken = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
        if (!string.IsNullOrEmpty(envToken))
        {
            return envToken;
        }

        // Priority 2: Token info with auto-refresh
        if (_tokenInfoStore is not null)
        {
            var tokenInfo = await _tokenInfoStore.GetTokenInfoAsync();
            if (tokenInfo is not null)
            {
                var now = DateTimeOffset.UtcNow;

                // Check if token needs refresh
                if (tokenInfo.IsExpired(now, ExpiryBuffer))
                {
                    var refreshedToken = await TryRefreshTokenAsync(tokenInfo, now);
                    if (refreshedToken is not null)
                    {
                        return refreshedToken.AccessToken;
                    }
                    // Refresh failed - if token is fully expired, return null
                    if (tokenInfo.IsExpired(now, TimeSpan.Zero))
                    {
                        return null;
                    }
                }

                return tokenInfo.AccessToken;
            }
        }

        // Priority 3: Simple stored token (no expiry tracking)
        return await _credentialStore.GetTokenAsync();
    }

    private async Task<TokenInfo?> TryRefreshTokenAsync(TokenInfo tokenInfo, DateTimeOffset now)
    {
        if (_deviceFlowAuth is null || tokenInfo.RefreshToken is null)
            return null;

        // Don't try to refresh if refresh token is expired
        if (tokenInfo.IsRefreshTokenExpired(now))
            return null;

        try
        {
            var result = await _deviceFlowAuth.RefreshTokenAsync(tokenInfo.RefreshToken);
            if (result.Success && result.TokenInfo is not null)
            {
                await _tokenInfoStore!.StoreTokenInfoAsync(result.TokenInfo);
                return result.TokenInfo;
            }
        }
        catch
        {
            // Refresh failed, fall back to existing token
        }

        return null;
    }

    public async Task<AuthStatus> GetStatusAsync()
    {
        // Check environment variable first
        var envToken = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
        if (!string.IsNullOrEmpty(envToken))
        {
            return new AuthStatus(true, Source: "environment variable (GITHUB_TOKEN)");
        }

        // Check token info store
        if (_tokenInfoStore is not null)
        {
            var tokenInfo = await _tokenInfoStore.GetTokenInfoAsync();
            if (tokenInfo is not null)
            {
                var now = DateTimeOffset.UtcNow;
                if (tokenInfo.IsExpired(now, TimeSpan.Zero) && 
                    (tokenInfo.RefreshToken is null || tokenInfo.IsRefreshTokenExpired(now)))
                {
                    return new AuthStatus(false);
                }
                return new AuthStatus(true, Source: "stored credentials");
            }
        }

        // Check simple stored token
        var storedToken = await _credentialStore.GetTokenAsync();
        if (!string.IsNullOrEmpty(storedToken))
        {
            return new AuthStatus(true, Source: "stored credentials");
        }

        return new AuthStatus(false);
    }

    public Task StoreTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Token cannot be empty", nameof(token));
        return _credentialStore.StoreTokenAsync(token);
    }

    public Task StoreTokenInfoAsync(TokenInfo tokenInfo)
    {
        ArgumentNullException.ThrowIfNull(tokenInfo);
        if (_tokenInfoStore is not null)
        {
            return _tokenInfoStore.StoreTokenInfoAsync(tokenInfo);
        }
        // Fall back to storing just the access token
        return _credentialStore.StoreTokenAsync(tokenInfo.AccessToken);
    }

    public async Task ClearAsync()
    {
        await _credentialStore.ClearAsync();
        if (_tokenInfoStore is not null)
        {
            await _tokenInfoStore.ClearAsync();
        }
    }
}
