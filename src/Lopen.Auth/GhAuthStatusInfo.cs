namespace Lopen.Auth;

/// <summary>
/// Parsed result from <c>gh auth status</c>.
/// </summary>
internal sealed record GhAuthStatusInfo(
    string Username,
    bool IsActive,
    string? TokenScopes = null);
