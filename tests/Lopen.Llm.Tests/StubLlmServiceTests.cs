using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public class StubLlmServiceTests
{
    [Fact]
    public async Task InvokeAsync_ThrowsLlmException()
    {
        var service = new StubLlmService(NullLogger<StubLlmService>.Instance);

        var ex = await Assert.ThrowsAsync<LlmException>(() =>
            service.InvokeAsync("prompt", "claude-opus-4.6", [], CancellationToken.None));

        Assert.Contains("Copilot SDK integration pending", ex.Message);
        Assert.Equal("claude-opus-4.6", ex.Model);
    }

    [Fact]
    public async Task InvokeAsync_ThrowsWithModelInException()
    {
        var service = new StubLlmService(NullLogger<StubLlmService>.Instance);

        var ex = await Assert.ThrowsAsync<LlmException>(() =>
            service.InvokeAsync("prompt", "gpt-5-mini", [], CancellationToken.None));

        Assert.Equal("gpt-5-mini", ex.Model);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StubLlmService(null!));
    }
}
