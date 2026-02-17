namespace Lopen.Auth;

/// <summary>
/// Immutable result of an authentication status check.
/// </summary>
public sealed record AuthStatusResult(
    AuthState State,
    AuthCredentialSource Source,
    string? Username = null,
    string? ErrorMessage = null);
