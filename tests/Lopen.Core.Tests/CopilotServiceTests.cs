using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

public class CopilotServiceTests
{
    [Fact]
    public async Task IsAvailableAsync_WhenAvailable_ReturnsTrue()
    {
        var service = new MockCopilotService { IsAvailable = true };

        var result = await service.IsAvailableAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenNotAvailable_ReturnsFalse()
    {
        var service = new MockCopilotService { IsAvailable = false };

        var result = await service.IsAvailableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthStatusAsync_WhenAuthenticated_ReturnsAuthenticatedStatus()
    {
        var service = new MockCopilotService
        {
            AuthStatus = new CopilotAuthStatus(true, "user", "testuser")
        };

        var status = await service.GetAuthStatusAsync();

        status.IsAuthenticated.Should().BeTrue();
        status.AuthType.Should().Be("user");
        status.Login.Should().Be("testuser");
    }

    [Fact]
    public async Task GetAuthStatusAsync_WhenNotAuthenticated_ReturnsNotAuthenticated()
    {
        var service = new MockCopilotService
        {
            AuthStatus = new CopilotAuthStatus(false)
        };

        var status = await service.GetAuthStatusAsync();

        status.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetModelsAsync_ReturnsAvailableModels()
    {
        var service = new MockCopilotService();

        var models = await service.GetModelsAsync();

        models.Should().Contain("gpt-5");
        models.Should().Contain("claude-sonnet-4.5");
    }

    [Fact]
    public async Task CreateSessionAsync_ReturnsSession()
    {
        var service = new MockCopilotService();

        await using var session = await service.CreateSessionAsync();

        session.Should().NotBeNull();
        session.SessionId.Should().StartWith("mock-session");
    }

    [Fact]
    public async Task CreateSessionAsync_WithCustomId_UsesProvidedId()
    {
        var service = new MockCopilotService();
        var options = new CopilotSessionOptions { SessionId = "custom-session-id" };

        await using var session = await service.CreateSessionAsync(options);

        session.SessionId.Should().Be("custom-session-id");
    }

    [Fact]
    public async Task CreateSessionAsync_WhenNotAvailable_ThrowsInvalidOperationException()
    {
        var service = new MockCopilotService { IsAvailable = false };

        var act = () => service.CreateSessionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResumeSessionAsync_WithValidId_ReturnsSession()
    {
        var service = new MockCopilotService();
        await using var created = await service.CreateSessionAsync();
        var sessionId = created.SessionId;

        await using var resumed = await service.ResumeSessionAsync(sessionId);

        resumed.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task ResumeSessionAsync_WithInvalidId_ThrowsInvalidOperationException()
    {
        var service = new MockCopilotService();

        var act = () => service.ResumeSessionAsync("nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResumeSessionAsync_WithEmptyId_ThrowsArgumentException()
    {
        var service = new MockCopilotService();

        var act = () => service.ResumeSessionAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsCreatedSessions()
    {
        var service = new MockCopilotService();
        await using var session1 = await service.CreateSessionAsync();
        await using var session2 = await service.CreateSessionAsync();

        var sessions = await service.ListSessionsAsync();

        sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSession()
    {
        var service = new MockCopilotService();
        await using var session = await service.CreateSessionAsync();
        var sessionId = session.SessionId;

        await service.DeleteSessionAsync(sessionId);

        var sessions = await service.ListSessionsAsync();
        sessions.Should().NotContain(s => s.SessionId == sessionId);
    }

    [Fact]
    public async Task DeleteSessionAsync_WithEmptyId_ThrowsArgumentException()
    {
        var service = new MockCopilotService();

        var act = () => service.DeleteSessionAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DisposeAsync_SetsWasDisposed()
    {
        var service = new MockCopilotService();

        await service.DisposeAsync();

        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task SessionsCreated_TracksSessionCount()
    {
        var service = new MockCopilotService();
        await using var _ = await service.CreateSessionAsync();
        await using var __ = await service.CreateSessionAsync();

        service.SessionsCreated.Should().Be(2);
    }
}
