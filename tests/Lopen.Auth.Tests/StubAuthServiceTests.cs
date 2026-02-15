using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Auth.Tests;

public class StubAuthServiceTests
{
    private static StubAuthService CreateService(Func<string, string?> envVarAccessor)
    {
        var resolver = new EnvironmentTokenSourceResolver(envVarAccessor);
        return new StubAuthService(resolver, NullLogger<StubAuthService>.Instance);
    }

    [Fact]
    public async Task GetStatusAsync_GhTokenSet_ReturnsAuthenticated()
    {
        var service = CreateService(name => name == "GH_TOKEN" ? "token" : null);

        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, status.State);
        Assert.Equal(AuthCredentialSource.GhToken, status.Source);
    }

    [Fact]
    public async Task GetStatusAsync_GitHubTokenSet_ReturnsAuthenticated()
    {
        var service = CreateService(name => name == "GITHUB_TOKEN" ? "token" : null);

        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, status.State);
        Assert.Equal(AuthCredentialSource.GitHubToken, status.Source);
    }

    [Fact]
    public async Task GetStatusAsync_NoTokens_ReturnsNotAuthenticated()
    {
        var service = CreateService(_ => null);

        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.NotAuthenticated, status.State);
        Assert.Equal(AuthCredentialSource.None, status.Source);
        Assert.NotNull(status.ErrorMessage);
        Assert.Contains("lopen auth login", status.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_TokenPresent_DoesNotThrow()
    {
        var service = CreateService(name => name == "GH_TOKEN" ? "token" : null);

        await service.ValidateAsync();
    }

    [Fact]
    public async Task ValidateAsync_NoToken_ThrowsAuthenticationException()
    {
        var service = CreateService(_ => null);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.ValidateAsync());
        Assert.Contains("GH_TOKEN", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_ThrowsAuthenticationException()
    {
        var service = CreateService(_ => null);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());
        Assert.Contains("GH_TOKEN", ex.Message);
    }

    [Fact]
    public async Task LogoutAsync_CompletesSuccessfully()
    {
        var service = CreateService(_ => null);

        await service.LogoutAsync();
    }
}
