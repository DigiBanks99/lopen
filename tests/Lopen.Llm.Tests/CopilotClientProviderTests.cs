using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public class CopilotClientProviderTests : IAsyncDisposable
{
    private CopilotClientProvider? _provider;

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }

    [Fact]
    public void Constructor_NullTokenProvider_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotClientProvider(null!, NullLogger<CopilotClientProvider>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotClientProvider(new NullGitHubTokenProvider(), null!));
    }

    [Fact]
    public void CreateClient_NoToken_CreatesClientWithoutExplicitToken()
    {
        _provider = new CopilotClientProvider(
            new NullGitHubTokenProvider(),
            NullLogger<CopilotClientProvider>.Instance);

        var client = _provider.CreateClient();

        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public void CreateClient_WithToken_CreatesClientWithToken()
    {
        var tokenProvider = new TestTokenProvider("test-github-token");
        _provider = new CopilotClientProvider(
            tokenProvider,
            NullLogger<CopilotClientProvider>.Instance);

        var client = _provider.CreateClient();

        Assert.NotNull(client);
        client.Dispose();
    }

    [Fact]
    public async Task GetClientAsync_AfterDispose_ThrowsObjectDisposed()
    {
        _provider = new CopilotClientProvider(
            new NullGitHubTokenProvider(),
            NullLogger<CopilotClientProvider>.Instance);

        await _provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.GetClientAsync());
    }

    [Fact]
    public async Task IsAuthenticatedAsync_AfterDispose_ThrowsObjectDisposed()
    {
        _provider = new CopilotClientProvider(
            new NullGitHubTokenProvider(),
            NullLogger<CopilotClientProvider>.Instance);

        await _provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.IsAuthenticatedAsync());
    }

    [Fact]
    public async Task DisposeAsync_MultipleDispose_DoesNotThrow()
    {
        _provider = new CopilotClientProvider(
            new NullGitHubTokenProvider(),
            NullLogger<CopilotClientProvider>.Instance);

        await _provider.DisposeAsync();
        await _provider.DisposeAsync(); // Should not throw
    }

    private sealed class TestTokenProvider(string token) : IGitHubTokenProvider
    {
        public string? GetToken() => token;
    }
}
