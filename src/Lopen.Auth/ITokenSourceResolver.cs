namespace Lopen.Auth;

/// <summary>
/// Resolves the credential source and token following the authentication precedence:
/// GH_TOKEN → GITHUB_TOKEN → SDK-stored credentials.
/// </summary>
public interface ITokenSourceResolver
{
    /// <summary>
    /// Determines the active credential source and returns the associated token value.
    /// Returns <see cref="AuthCredentialSource.None"/> with a null token when no source is available.
    /// </summary>
    TokenSourceResult Resolve();
}
