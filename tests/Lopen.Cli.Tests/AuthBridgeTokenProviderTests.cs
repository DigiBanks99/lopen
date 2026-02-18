using Lopen.Auth;
using Lopen.Llm;

namespace Lopen.Cli.Tests;

public class AuthBridgeTokenProviderTests
{
    [Fact]
    public void Constructor_NullResolver_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthBridgeTokenProvider(null!));
    }

    [Fact]
    public void GetToken_ReturnsTokenFromResolver()
    {
        var resolver = new FakeTokenSourceResolver(
            new TokenSourceResult(AuthCredentialSource.GhToken, "my-token"));

        var provider = new AuthBridgeTokenProvider(resolver);

        Assert.Equal("my-token", provider.GetToken());
    }

    [Fact]
    public void GetToken_ReturnsNullWhenNoToken()
    {
        var resolver = new FakeTokenSourceResolver(
            new TokenSourceResult(AuthCredentialSource.None, null));

        var provider = new AuthBridgeTokenProvider(resolver);

        Assert.Null(provider.GetToken());
    }

    [Fact]
    public void GetToken_ReturnsTokenFromEnvironmentVariable()
    {
        var resolver = new FakeTokenSourceResolver(
            new TokenSourceResult(AuthCredentialSource.GitHubToken, "token-value"));

        var provider = new AuthBridgeTokenProvider(resolver);

        Assert.Equal("token-value", provider.GetToken());
    }

    private sealed class FakeTokenSourceResolver : ITokenSourceResolver
    {
        private readonly TokenSourceResult _result;

        public FakeTokenSourceResolver(TokenSourceResult result) => _result = result;

        public TokenSourceResult Resolve() => _result;
    }
}
