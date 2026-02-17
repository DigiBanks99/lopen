namespace Lopen.Auth.Tests;

public class EnvironmentTokenSourceResolverTests
{
    [Fact]
    public void Resolve_GhTokenSet_ReturnsGhTokenSource()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GH_TOKEN" => "gho_test_token_123",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.Equal("gho_test_token_123", result.Token);
    }

    [Fact]
    public void Resolve_GitHubTokenSet_ReturnsGitHubTokenSource()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GITHUB_TOKEN" => "github_pat_abc123",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.GitHubToken, result.Source);
        Assert.Equal("github_pat_abc123", result.Token);
    }

    [Fact]
    public void Resolve_BothTokensSet_GhTokenTakesPrecedence()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GH_TOKEN" => "gh_token_value",
            "GITHUB_TOKEN" => "github_token_value",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.Equal("gh_token_value", result.Token);
    }

    [Fact]
    public void Resolve_NoTokensSet_ReturnsNoneSource()
    {
        var resolver = new EnvironmentTokenSourceResolver(_ => null);

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.None, result.Source);
        Assert.Null(result.Token);
    }

    [Fact]
    public void Resolve_EmptyGhToken_FallsThrough()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GH_TOKEN" => "",
            "GITHUB_TOKEN" => "fallback_token",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.GitHubToken, result.Source);
        Assert.Equal("fallback_token", result.Token);
    }

    [Fact]
    public void Resolve_EmptyBothTokens_ReturnsNone()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GH_TOKEN" => "",
            "GITHUB_TOKEN" => "",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.None, result.Source);
        Assert.Null(result.Token);
    }

    [Fact]
    public void Resolve_WhitespaceGhToken_FallsThrough()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GH_TOKEN" => "   ",
            "GITHUB_TOKEN" => "valid_token",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.GitHubToken, result.Source);
        Assert.Equal("valid_token", result.Token);
    }

    [Fact]
    public void Constructor_NullAccessor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EnvironmentTokenSourceResolver(null!));
    }

    [Fact]
    public void DefaultConstructor_UsesRealEnvironment()
    {
        // Should not throw — validates default constructor wiring
        var resolver = new EnvironmentTokenSourceResolver();
        var result = resolver.Resolve();

        // Result depends on actual environment, but should not be null
        Assert.NotNull(result);
    }
}
