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
    /// Clears stored credentials.
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// Authentication service supporting environment variable and file-based storage.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ICredentialStore _credentialStore;
    private const string GitHubTokenEnvVar = "GITHUB_TOKEN";

    public AuthService(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
    }

    public async Task<string?> GetTokenAsync()
    {
        // Priority 1: Environment variable
        var envToken = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
        if (!string.IsNullOrEmpty(envToken))
        {
            return envToken;
        }

        // Priority 2: Stored token
        return await _credentialStore.GetTokenAsync();
    }

    public async Task<AuthStatus> GetStatusAsync()
    {
        // Check environment variable first
        var envToken = Environment.GetEnvironmentVariable(GitHubTokenEnvVar);
        if (!string.IsNullOrEmpty(envToken))
        {
            return new AuthStatus(true, Source: "environment variable (GITHUB_TOKEN)");
        }

        // Check stored token
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

    public Task ClearAsync()
    {
        return _credentialStore.ClearAsync();
    }
}
