namespace Lopen.Auth;

/// <summary>
/// Manages authentication lifecycle for Copilot SDK access.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Initiates interactive authentication via the Copilot SDK device flow.
    /// Not applicable in headless mode.
    /// </summary>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears SDK-managed credentials and confirms removal.
    /// Warns if environment variables are still set.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and returns the current authentication state.
    /// </summary>
    Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-flight check that validates credentials are present and valid.
    /// Throws <see cref="AuthenticationException"/> on failure.
    /// </summary>
    Task ValidateAsync(CancellationToken cancellationToken = default);
}
