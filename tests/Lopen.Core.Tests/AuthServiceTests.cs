using FluentAssertions;
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

            token.Should().Be("test-env-token");
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

        token.Should().Be("stored-token");
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

            status.IsAuthenticated.Should().BeTrue();
            status.Source.Should().Contain("environment variable");
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

        status.IsAuthenticated.Should().BeTrue();
        status.Source.Should().Contain("stored credentials");
    }

    [Fact]
    public async Task GetStatusAsync_NoToken_ReturnsNotAuthenticated()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store);

        var status = await service.GetStatusAsync();

        status.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task StoreTokenAsync_StoresToken()
    {
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store);

        await service.StoreTokenAsync("my-token");

        var stored = await store.GetTokenAsync();
        stored.Should().Be("my-token");
    }

    [Fact]
    public async Task ClearAsync_ClearsStoredToken()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreTokenAsync("my-token");
        var service = new AuthService(store);

        await service.ClearAsync();

        var stored = await store.GetTokenAsync();
        stored.Should().BeNull();
    }

    [Fact]
    public void StoreTokenAsync_WithEmptyToken_ThrowsArgumentException()
    {
        var store = new InMemoryCredentialStore();
        var service = new AuthService(store);

        Func<Task> act = () => service.StoreTokenAsync("");

        act.Should().ThrowAsync<ArgumentException>();
    }
}

/// <summary>
/// In-memory credential store for testing.
/// </summary>
internal class InMemoryCredentialStore : ICredentialStore
{
    private string? _token;

    public Task<string?> GetTokenAsync() => Task.FromResult(_token);
    public Task StoreTokenAsync(string token)
    {
        _token = token;
        return Task.CompletedTask;
    }
    public Task ClearAsync()
    {
        _token = null;
        return Task.CompletedTask;
    }
}
