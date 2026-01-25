using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class CopilotServiceTests
{
    [Fact]
    public async Task IsAvailableAsync_WhenAvailable_ReturnsTrue()
    {
        var service = new MockCopilotService { IsAvailable = true };

        var result = await service.IsAvailableAsync();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenNotAvailable_ReturnsFalse()
    {
        var service = new MockCopilotService { IsAvailable = false };

        var result = await service.IsAvailableAsync();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAuthStatusAsync_WhenAuthenticated_ReturnsAuthenticatedStatus()
    {
        var service = new MockCopilotService
        {
            AuthStatus = new CopilotAuthStatus(true, "user", "testuser")
        };

        var status = await service.GetAuthStatusAsync();

        status.IsAuthenticated.ShouldBeTrue();
        status.AuthType.ShouldBe("user");
        status.Login.ShouldBe("testuser");
    }

    [Fact]
    public async Task GetAuthStatusAsync_WhenNotAuthenticated_ReturnsNotAuthenticated()
    {
        var service = new MockCopilotService
        {
            AuthStatus = new CopilotAuthStatus(false)
        };

        var status = await service.GetAuthStatusAsync();

        status.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetModelsAsync_ReturnsAvailableModels()
    {
        var service = new MockCopilotService();

        var models = await service.GetModelsAsync();

        models.ShouldContain("gpt-5");
        models.ShouldContain("claude-sonnet-4.5");
    }

    [Fact]
    public async Task CreateSessionAsync_ReturnsSession()
    {
        var service = new MockCopilotService();

        await using var session = await service.CreateSessionAsync();

        session.ShouldNotBeNull();
        session.SessionId.ShouldStartWith("mock-session");
    }

    [Fact]
    public async Task CreateSessionAsync_WithCustomId_UsesProvidedId()
    {
        var service = new MockCopilotService();
        var options = new CopilotSessionOptions { SessionId = "custom-session-id" };

        await using var session = await service.CreateSessionAsync(options);

        session.SessionId.ShouldBe("custom-session-id");
    }

    [Fact]
    public async Task CreateSessionAsync_WhenNotAvailable_ThrowsInvalidOperationException()
    {
        var service = new MockCopilotService { IsAvailable = false };

        var act = () => service.CreateSessionAsync();

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task ResumeSessionAsync_WithValidId_ReturnsSession()
    {
        var service = new MockCopilotService();
        await using var created = await service.CreateSessionAsync();
        var sessionId = created.SessionId;

        await using var resumed = await service.ResumeSessionAsync(sessionId);

        resumed.SessionId.ShouldBe(sessionId);
    }

    [Fact]
    public async Task ResumeSessionAsync_WithInvalidId_ThrowsInvalidOperationException()
    {
        var service = new MockCopilotService();

        var act = () => service.ResumeSessionAsync("nonexistent");

        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task ResumeSessionAsync_WithEmptyId_ThrowsArgumentException()
    {
        var service = new MockCopilotService();

        var act = () => service.ResumeSessionAsync("");

        await Should.ThrowAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsCreatedSessions()
    {
        var service = new MockCopilotService();
        await using var session1 = await service.CreateSessionAsync();
        await using var session2 = await service.CreateSessionAsync();

        var sessions = await service.ListSessionsAsync();

        sessions.Count().ShouldBe(2);
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        var service = new MockCopilotService();
        await using var session = await service.CreateSessionAsync();
        var sessionId = session.SessionId;

        await service.DeleteSessionAsync(sessionId);

        var sessions = await service.ListSessionsAsync();
        sessions.ShouldNotContain(s => s.SessionId == sessionId);
    }

    [Fact]
    public async Task DeleteSessionAsync_WithEmptyId_ThrowsArgumentException()
    {
        var service = new MockCopilotService();

        var act = () => service.DeleteSessionAsync("");

        await Should.ThrowAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task DisposeAsync_SetsWasDisposed()
    {
        var service = new MockCopilotService();

        await service.DisposeAsync();

        service.WasDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task SessionsCreated_TracksSessionCount()
    {
        var service = new MockCopilotService();
        await using var _ = await service.CreateSessionAsync();
        await using var __ = await service.CreateSessionAsync();

        service.SessionsCreated.ShouldBe(2);
    }
}
