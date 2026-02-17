namespace Lopen.Auth;

/// <summary>
/// Represents the current authentication state.
/// </summary>
public enum AuthState
{
    /// <summary>Valid credentials are available.</summary>
    Authenticated,

    /// <summary>No credentials found.</summary>
    NotAuthenticated,

    /// <summary>Credentials exist but are invalid or expired.</summary>
    InvalidCredentials,
}
