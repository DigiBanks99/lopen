namespace Lopen.Auth;

/// <summary>
/// Identifies where the active credential originated.
/// </summary>
public enum AuthCredentialSource
{
    /// <summary>No credential source available.</summary>
    None,

    /// <summary>Credential from the GH_TOKEN environment variable.</summary>
    GhToken,

    /// <summary>Credential from the GITHUB_TOKEN environment variable.</summary>
    GitHubToken,

    /// <summary>Credential stored by the Copilot SDK (device flow).</summary>
    SdkCredentials,
}
