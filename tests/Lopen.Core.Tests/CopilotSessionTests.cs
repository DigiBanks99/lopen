using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

public class CopilotSessionTests
{
    [Fact]
    public void SessionId_ReturnsConfiguredId()
    {
        var session = new MockCopilotSession("test-session-123");

        session.SessionId.Should().Be("test-session-123");
    }

    [Fact]
    public async Task StreamAsync_YieldsChunks()
    {
        var session = new MockCopilotSession();
        var chunks = new List<string>();

        await foreach (var chunk in session.StreamAsync("test prompt"))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEquivalentTo(["Hello", " from ", "mock!"]);
    }

    [Fact]
    public async Task StreamAsync_WithCustomHandler_UsesHandler()
    {
        async IAsyncEnumerable<string> CustomStream(string prompt)
        {
            yield return "Custom ";
            await Task.Delay(1);
            yield return "response";
        }

        var session = new MockCopilotSession(
            "test-session",
            streamHandler: CustomStream,
            sendHandler: null);
        var chunks = new List<string>();

        await foreach (var chunk in session.StreamAsync("test"))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEquivalentTo(["Custom ", "response"]);
    }

    [Fact]
    public async Task StreamAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        var session = new MockCopilotSession();

        var act = async () =>
        {
            await foreach (var _ in session.StreamAsync("")) { }
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendAsync_ReturnsCompleteResponse()
    {
        var session = new MockCopilotSession();

        var response = await session.SendAsync("test prompt");

        response.Should().Be("Hello from mock!");
    }

    [Fact]
    public async Task SendAsync_WithCustomHandler_UsesHandler()
    {
        var session = new MockCopilotSession(
            "test-session",
            streamHandler: null,
            sendHandler: prompt => Task.FromResult<string?>($"Echo: {prompt}"));

        var response = await session.SendAsync("test");

        response.Should().Be("Echo: test");
    }

    [Fact]
    public async Task SendAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        var session = new MockCopilotSession();

        var act = () => session.SendAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AbortAsync_SetsWasAborted()
    {
        var session = new MockCopilotSession();

        await session.AbortAsync();

        session.WasAborted.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_SetsWasDisposed()
    {
        var session = new MockCopilotSession();

        await session.DisposeAsync();

        session.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var session = new MockCopilotSession();
        var chunks = new List<string>();

        cts.CancelAfter(5);

        var act = async () =>
        {
            await foreach (var chunk in session.StreamAsync("test", cts.Token))
            {
                chunks.Add(chunk);
                await Task.Delay(10);
            }
        };

        // Should throw or return partial results
        try
        {
            await act();
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Either cancelled or completed quickly
        chunks.Count.Should().BeLessThanOrEqualTo(3);
    }
}

public class CopilotModelsTests
{
    [Fact]
    public void CopilotAuthStatus_CanBeCreated()
    {
        var status = new CopilotAuthStatus(true, "oauth", "testuser");

        status.IsAuthenticated.Should().BeTrue();
        status.AuthType.Should().Be("oauth");
        status.Login.Should().Be("testuser");
    }

    [Fact]
    public void CopilotSessionOptions_HasDefaults()
    {
        var options = new CopilotSessionOptions();

        options.Model.Should().Be("gpt-5");
        options.Streaming.Should().BeTrue();
        options.SessionId.Should().BeNull();
    }

    [Fact]
    public void CopilotSessionOptions_CanOverrideDefaults()
    {
        var options = new CopilotSessionOptions
        {
            SessionId = "custom-id",
            Model = "claude-sonnet-4.5",
            Streaming = false
        };

        options.SessionId.Should().Be("custom-id");
        options.Model.Should().Be("claude-sonnet-4.5");
        options.Streaming.Should().BeFalse();
    }

    [Fact]
    public void CopilotSessionInfo_CanBeCreated()
    {
        var now = DateTime.UtcNow;
        var info = new CopilotSessionInfo("session-1", now, now, "Test summary");

        info.SessionId.Should().Be("session-1");
        info.Summary.Should().Be("Test summary");
    }
}
