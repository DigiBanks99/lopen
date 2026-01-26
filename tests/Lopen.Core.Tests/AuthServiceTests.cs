using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task GetTokenAsync_WithEnvironmentVariable_ReturnsEnvToken()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-env-token");
        try
        {
            var store = new InMemoryCredentialStore();
            var service = new AuthService(store);

            var token = await service.GetTokenAsync();

            token.ShouldBe("test-env-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetTokenAsync_WithoutEnvVar_ReturnsStoredToken()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        await store.StoreTokenAsync("stored-token");
        var service = new AuthService(store);

        var token = await service.GetTokenAsync();

        token.ShouldBe("stored-token");
    }

    [Fact]
    public async Task GetStatusAsync_WithEnvVar_ReturnsAuthenticatedWithSource()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token");
        try
        {
            var store = new InMemoryCredentialStore();
            var service = new AuthService(store);

            var status = await service.GetStatusAsync();

            status.IsAuthenticated.ShouldBeTrue();
            status.Source.ShouldNotBeNull();
            status.Source.ShouldContain("environment variable");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetStatusAsync_WithStoredToken_ReturnsAuthenticatedWithSource()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        await store.StoreTokenAsync("stored-token");
        var service = new AuthService(store);

        var status = await service.GetStatusAsync();

        status.IsAuthenticated.ShouldBeTrue();
        status.Source.ShouldNotBeNull();
        status.Source.ShouldContain("stored credentials");
    }

    [Fact]
    public async Task GetStatusAsync_NoToken_ReturnsNotAuthenticated()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store);

        var status = await service.GetStatusAsync();

        status.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task StoreTokenAsync_StoresToken()
    {
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store);

        await service.StoreTokenAsync("my-token");

        var stored = await store.GetTokenAsync();
        stored.ShouldBe("my-token");
    }

    [Fact]
    public async Task ClearAsync_ClearsStoredToken()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreTokenAsync("my-token");
        var service = new AuthService(store);

        await service.ClearAsync();

        var stored = await store.GetTokenAsync();
        stored.ShouldBeNull();
    }

    [Fact]
    public async Task StoreTokenAsync_WithEmptyToken_ThrowsArgumentException()
    {
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store);

        Func<Task> act = () => service.StoreTokenAsync("");

        await Should.ThrowAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task GetTokenAsync_WithValidTokenInfo_ReturnsAccessToken()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var tokenInfo = new TokenInfo
        {
            AccessToken = "access-token-123",
            RefreshToken = "refresh-token-456",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await store.StoreTokenInfoAsync(tokenInfo);
        var service = new AuthService(store, store);

        var token = await service.GetTokenAsync();

        token.ShouldBe("access-token-123");
    }

    [Fact]
    public async Task GetTokenAsync_WithExpiredToken_RefreshesToken()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var expiredTokenInfo = new TokenInfo
        {
            AccessToken = "expired-token",
            RefreshToken = "refresh-token-456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10), // Already expired
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddMonths(5) // Refresh token valid
        };
        await store.StoreTokenInfoAsync(expiredTokenInfo);

        var mockDeviceFlow = new MockDeviceFlowAuth();
        mockDeviceFlow.WithConfig(new OAuthAppConfig { ClientId = "test-client" });
        mockDeviceFlow.WithRefreshResult(new RefreshTokenResult
        {
            Success = true,
            TokenInfo = new TokenInfo
            {
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
            }
        });

        var service = new AuthService(store, store, mockDeviceFlow);

        var token = await service.GetTokenAsync();

        token.ShouldBe("new-access-token");
        mockDeviceFlow.RefreshWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTokenAsync_WithExpiredTokenAndNoRefreshToken_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var expiredTokenInfo = new TokenInfo
        {
            AccessToken = "expired-token",
            RefreshToken = null, // No refresh token
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        await store.StoreTokenInfoAsync(expiredTokenInfo);

        var service = new AuthService(store, store);

        var token = await service.GetTokenAsync();

        token.ShouldBeNull();
    }

    [Fact]
    public async Task GetTokenAsync_WithTokenNearExpiry_RefreshesProactively()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var nearExpiryTokenInfo = new TokenInfo
        {
            AccessToken = "near-expiry-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3), // Within 5 min buffer
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddMonths(5)
        };
        await store.StoreTokenInfoAsync(nearExpiryTokenInfo);

        var mockDeviceFlow = new MockDeviceFlowAuth();
        mockDeviceFlow.WithConfig(new OAuthAppConfig { ClientId = "test-client" });
        mockDeviceFlow.WithRefreshResult(new RefreshTokenResult
        {
            Success = true,
            TokenInfo = new TokenInfo
            {
                AccessToken = "refreshed-token",
                RefreshToken = "new-refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
            }
        });

        var service = new AuthService(store, store, mockDeviceFlow);

        var token = await service.GetTokenAsync();

        token.ShouldBe("refreshed-token");
        mockDeviceFlow.RefreshWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTokenAsync_WithFailedRefresh_ReturnsValidTokenIfNotFullyExpired()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var nearExpiryTokenInfo = new TokenInfo
        {
            AccessToken = "near-expiry-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3), // Within buffer but still valid
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddMonths(5)
        };
        await store.StoreTokenInfoAsync(nearExpiryTokenInfo);

        var mockDeviceFlow = new MockDeviceFlowAuth();
        mockDeviceFlow.WithConfig(new OAuthAppConfig { ClientId = "test-client" });
        mockDeviceFlow.WithRefreshResult(new RefreshTokenResult
        {
            Success = false,
            Error = "Network error"
        });

        var service = new AuthService(store, store, mockDeviceFlow);

        var token = await service.GetTokenAsync();

        // Should return existing token since it's still valid (just near expiry)
        token.ShouldBe("near-expiry-token");
    }

    [Fact]
    public async Task GetTokenAsync_WithExpiredRefreshToken_DoesNotAttemptRefresh()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var expiredTokenInfo = new TokenInfo
        {
            AccessToken = "expired-token",
            RefreshToken = "expired-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // Refresh token also expired
        };
        await store.StoreTokenInfoAsync(expiredTokenInfo);

        var mockDeviceFlow = new MockDeviceFlowAuth();
        mockDeviceFlow.WithConfig(new OAuthAppConfig { ClientId = "test-client" });

        var service = new AuthService(store, store, mockDeviceFlow);

        var token = await service.GetTokenAsync();

        token.ShouldBeNull();
        mockDeviceFlow.RefreshWasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task StoreTokenInfoAsync_StoresTokenInfo()
    {
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store, store);
        var tokenInfo = new TokenInfo
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8)
        };

        await service.StoreTokenInfoAsync(tokenInfo);

        var stored = await store.GetTokenInfoAsync();
        stored.ShouldNotBeNull();
        stored.AccessToken.ShouldBe("access-token");
        stored.RefreshToken.ShouldBe("refresh-token");
    }
}

public class TokenInfoTests
{
    [Fact]
    public void FromResponse_CreatesTokenInfo()
    {
        var response = new TokenResponse
        {
            AccessToken = "ghu_abc123",
            RefreshToken = "ghr_xyz789",
            ExpiresIn = 28800,
            RefreshTokenExpiresIn = 15897600
        };
        var now = DateTimeOffset.UtcNow;

        var tokenInfo = TokenInfo.FromResponse(response, now);

        tokenInfo.AccessToken.ShouldBe("ghu_abc123");
        tokenInfo.RefreshToken.ShouldBe("ghr_xyz789");
        tokenInfo.ExpiresAt.ShouldNotBeNull();
        tokenInfo.ExpiresAt!.Value.ShouldBeInRange(now.AddSeconds(28799), now.AddSeconds(28801));
        tokenInfo.RefreshTokenExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public void FromResponse_WithNullAccessToken_ThrowsArgumentException()
    {
        var response = new TokenResponse { AccessToken = null };
        var now = DateTimeOffset.UtcNow;

        Should.Throw<ArgumentException>(() => TokenInfo.FromResponse(response, now));
    }

    [Fact]
    public void IsExpired_WithNoExpiry_ReturnsFalse()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            ExpiresAt = null
        };

        tokenInfo.IsExpired(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WithFutureExpiry_ReturnsFalse()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        tokenInfo.IsExpired(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WithPastExpiry_ReturnsTrue()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        tokenInfo.IsExpired(DateTimeOffset.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_WithBuffer_ReturnsTrue_WhenWithinBuffer()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3)
        };

        tokenInfo.IsExpired(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)).ShouldBeTrue();
    }

    [Fact]
    public void IsRefreshTokenExpired_WithNoRefreshToken_ReturnsTrue()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            RefreshToken = null
        };

        tokenInfo.IsRefreshTokenExpired(DateTimeOffset.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void IsRefreshTokenExpired_WithValidRefreshToken_ReturnsFalse()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            RefreshToken = "refresh",
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddMonths(5)
        };

        tokenInfo.IsRefreshTokenExpired(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void IsRefreshTokenExpired_WithExpiredRefreshToken_ReturnsTrue()
    {
        var tokenInfo = new TokenInfo
        {
            AccessToken = "token",
            RefreshToken = "refresh",
            RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        tokenInfo.IsRefreshTokenExpired(DateTimeOffset.UtcNow).ShouldBeTrue();
    }
}

/// <summary>
/// In-memory credential store for testing.
/// </summary>
internal class InMemoryCredentialStore : ICredentialStore, ITokenInfoStore
{
    private string? _token;
    private TokenInfo? _tokenInfo;

    public Task<string?> GetTokenAsync() => Task.FromResult(_token);
    public Task<TokenInfo?> GetTokenInfoAsync() => Task.FromResult(_tokenInfo);
    public Task StoreTokenAsync(string token)
    {
        _token = token;
        return Task.CompletedTask;
    }
    public Task StoreTokenInfoAsync(TokenInfo tokenInfo)
    {
        _tokenInfo = tokenInfo;
        _token = tokenInfo.AccessToken;
        return Task.CompletedTask;
    }
    public Task ClearAsync()
    {
        _token = null;
        _tokenInfo = null;
        return Task.CompletedTask;
    }
}
