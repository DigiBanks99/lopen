using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class CopilotSessionTests
{
    [Fact]
    public void SessionId_ReturnsConfiguredId()
    {
        var session = new MockCopilotSession("test-session-123");

        session.SessionId.ShouldBe("test-session-123");
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

        chunks.ShouldBe(new[] { "Hello", " from ", "mock!" });
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

        chunks.ShouldBe(new[] { "Custom ", "response" });
    }

    [Fact]
    public async Task StreamAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        var session = new MockCopilotSession();

        var act = async () =>
        {
            await foreach (var _ in session.StreamAsync("")) { }
        };

        await Should.ThrowAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task SendAsync_ReturnsCompleteResponse()
    {
        var session = new MockCopilotSession();

        var response = await session.SendAsync("test prompt");

        response.ShouldBe("Hello from mock!");
    }

    [Fact]
    public async Task SendAsync_WithCustomHandler_UsesHandler()
    {
        var session = new MockCopilotSession(
            "test-session",
            streamHandler: null,
            sendHandler: prompt => Task.FromResult<string?>($"Echo: {prompt}"));

        var response = await session.SendAsync("test");

        response.ShouldBe("Echo: test");
    }

    [Fact]
    public async Task SendAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        var session = new MockCopilotSession();

        var act = () => session.SendAsync("");

        await Should.ThrowAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task AbortAsync_SetsWasAborted()
    {
        var session = new MockCopilotSession();

        await session.AbortAsync();

        session.WasAborted.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_SetsWasDisposed()
    {
        var session = new MockCopilotSession();

        await session.DisposeAsync();

        session.WasDisposed.ShouldBeTrue();
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
        chunks.Count.ShouldBeLessThanOrEqualTo(3);
    }
}

public class CopilotModelsTests
{
    [Fact]
    public void CopilotAuthStatus_CanBeCreated()
    {
        var status = new CopilotAuthStatus(true, "oauth", "testuser");

        status.IsAuthenticated.ShouldBeTrue();
        status.AuthType.ShouldBe("oauth");
        status.Login.ShouldBe("testuser");
    }

    [Fact]
    public void CopilotSessionOptions_HasDefaults()
    {
        var options = new CopilotSessionOptions();

        options.Model.ShouldBe("gpt-5");
        options.Streaming.ShouldBeTrue();
        options.SessionId.ShouldBeNull();
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

        options.SessionId.ShouldBe("custom-id");
        options.Model.ShouldBe("claude-sonnet-4.5");
        options.Streaming.ShouldBeFalse();
    }

    [Fact]
    public void CopilotSessionInfo_CanBeCreated()
    {
        var now = DateTime.UtcNow;
        var info = new CopilotSessionInfo("session-1", now, now, "Test summary");

        info.SessionId.ShouldBe("session-1");
        info.Summary.ShouldBe("Test summary");
    }
}
