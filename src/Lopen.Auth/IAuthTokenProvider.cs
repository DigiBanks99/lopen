namespace Lopen.Auth;

/// <summary>
/// Provides a GitHub token for Copilot SDK authentication.
/// </summary>
public interface IAuthTokenProvider
{
    /// <summary>
    /// Returns the GitHub token if available from environment variables,
    /// or null to let the SDK resolve credentials from its built-in chain
    /// (including gh CLI stored credentials).
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}
