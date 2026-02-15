namespace Lopen.Auth;

/// <summary>
/// Resolves token source from environment variables following precedence:
/// GH_TOKEN (highest) → GITHUB_TOKEN → None (fall through to SDK credentials).
/// </summary>
public sealed class EnvironmentTokenSourceResolver : ITokenSourceResolver
{
    internal const string GhTokenVariable = "GH_TOKEN";
    internal const string GitHubTokenVariable = "GITHUB_TOKEN";

    private readonly Func<string, string?> _getEnvironmentVariable;

    public EnvironmentTokenSourceResolver()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>
    /// Constructor accepting a custom environment variable accessor for testability.
    /// </summary>
    internal EnvironmentTokenSourceResolver(Func<string, string?> getEnvironmentVariable)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    }

    public TokenSourceResult Resolve()
    {
        var ghToken = _getEnvironmentVariable(GhTokenVariable);
        if (!string.IsNullOrWhiteSpace(ghToken))
        {
            return new TokenSourceResult(AuthCredentialSource.GhToken, ghToken);
        }

        var githubToken = _getEnvironmentVariable(GitHubTokenVariable);
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            return new TokenSourceResult(AuthCredentialSource.GitHubToken, githubToken);
        }

        return new TokenSourceResult(AuthCredentialSource.None, null);
    }
}
