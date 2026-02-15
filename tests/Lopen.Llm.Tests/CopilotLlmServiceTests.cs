using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public class CopilotLlmServiceTests
{
    private static CopilotLlmService CreateService(ICopilotClientProvider? clientProvider = null)
    {
        return new CopilotLlmService(
            clientProvider ?? new FakeClientProvider(),
            NullLogger<CopilotLlmService>.Instance);
    }

    [Fact]
    public void Constructor_NullClientProvider_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotLlmService(null!, NullLogger<CopilotLlmService>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotLlmService(new FakeClientProvider(), null!));
    }

    [Fact]
    public async Task InvokeAsync_NullSystemPrompt_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.InvokeAsync(null!, "model", []));
    }

    [Fact]
    public async Task InvokeAsync_EmptySystemPrompt_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InvokeAsync("", "model", []));
    }

    [Fact]
    public async Task InvokeAsync_NullModel_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.InvokeAsync("prompt", null!, []));
    }

    [Fact]
    public async Task InvokeAsync_EmptyModel_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InvokeAsync("prompt", "", []));
    }

    [Fact]
    public async Task InvokeAsync_NullTools_ThrowsArgumentNull()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.InvokeAsync("prompt", "model", null!));
    }

    [Fact]
    public async Task InvokeAsync_ClientProviderFails_ThrowsLlmException()
    {
        var failingProvider = new FailingClientProvider();
        var service = CreateService(failingProvider);

        var ex = await Assert.ThrowsAsync<LlmException>(() =>
            service.InvokeAsync("prompt", "claude-sonnet-4", []));

        Assert.Contains("Failed to get Copilot client", ex.Message);
        Assert.Equal("claude-sonnet-4", ex.Model);
    }

    [Fact]
    public async Task InvokeAsync_ClientProviderThrowsLlmException_PropagatesDirectly()
    {
        var failingProvider = new LlmExceptionClientProvider();
        var service = CreateService(failingProvider);

        var ex = await Assert.ThrowsAsync<LlmException>(() =>
            service.InvokeAsync("prompt", "claude-sonnet-4", []));

        Assert.Equal("SDK auth failed", ex.Message);
    }

    [Theory]
    [InlineData("claude-opus-4.6", true)]
    [InlineData("claude-opus-4.5", true)]
    [InlineData("gpt-5", true)]
    [InlineData("gpt-5-mini", true)]
    [InlineData("o3-pro", true)]
    [InlineData("o1", true)]
    [InlineData("claude-sonnet-4", false)]
    [InlineData("gpt-4.1", false)]
    [InlineData("claude-haiku-3.5", false)]
    [InlineData("gemini-3-pro", false)]
    public void IsPremiumModel_IdentifiesCorrectly(string model, bool expected)
    {
        Assert.Equal(expected, CopilotLlmService.IsPremiumModel(model));
    }

    /// <summary>
    /// Fake client provider that fails with a generic exception.
    /// </summary>
    private sealed class FailingClientProvider : ICopilotClientProvider
    {
        public Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Connection failed");

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Fake client provider that throws LlmException directly.
    /// </summary>
    private sealed class LlmExceptionClientProvider : ICopilotClientProvider
    {
        public Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
            => throw new LlmException("SDK auth failed", "claude-sonnet-4");

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Fake client provider that returns null (used when we expect param validation to fire first).
    /// </summary>
    private sealed class FakeClientProvider : ICopilotClientProvider
    {
        public Task<CopilotClient> GetClientAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Should not be called");

        public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
