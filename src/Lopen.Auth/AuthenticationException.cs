namespace Lopen.Auth;

/// <summary>
/// Thrown when authentication validation fails.
/// Includes what failed, why, and how to fix per the auth spec error message requirements.
/// </summary>
public sealed class AuthenticationException : Exception
{
    public AuthenticationException(string message)
        : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
