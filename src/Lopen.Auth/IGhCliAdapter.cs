namespace Lopen.Auth;

/// <summary>
/// Wraps GitHub CLI (gh) operations for authentication.
/// Provides a testable abstraction over the gh auth subcommands.
/// </summary>
internal interface IGhCliAdapter
{
    /// <summary>
    /// Checks whether the gh CLI is installed and available on PATH.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <c>gh auth login</c> with device flow via web browser.
    /// Returns the authenticated username on success.
    /// </summary>
    /// <exception cref="AuthenticationException">Thrown when login fails or is cancelled.</exception>
    Task<string> LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <c>gh auth status</c> and parses the result.
    /// Returns null when no credentials are stored.
    /// </summary>
    Task<GhAuthStatusInfo?> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <c>gh auth logout</c> to clear stored credentials.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether current credentials are functional by making a lightweight API call.
    /// Returns true if credentials are valid, false if invalid/expired.
    /// </summary>
    Task<bool> ValidateCredentialsAsync(CancellationToken cancellationToken = default);
}
