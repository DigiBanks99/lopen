namespace Lopen.Auth;

/// <summary>
/// Result of token source resolution containing the resolved credential source and token value.
/// </summary>
public sealed record TokenSourceResult(
    AuthCredentialSource Source,
    string? Token);
