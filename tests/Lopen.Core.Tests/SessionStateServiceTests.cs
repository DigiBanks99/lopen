using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

/// <summary>
/// Mock auth service for testing.
/// </summary>
public class MockAuthService : IAuthService
{
    private AuthStatus _status;
    private string? _token;

    public MockAuthService(bool isAuthenticated = false, string? username = null, string? source = null)
    {
        _status = new AuthStatus(isAuthenticated, username, source);
    }

    public void SetStatus(bool isAuthenticated, string? username = null, string? source = null)
    {
        _status = new AuthStatus(isAuthenticated, username, source);
    }

    public void SetToken(string? token) => _token = token;

    public Task<AuthStatus> GetStatusAsync() => Task.FromResult(_status);
    public Task<string?> GetTokenAsync() => Task.FromResult(_token);
    public Task StoreTokenAsync(string token) { _token = token; return Task.CompletedTask; }
    public Task ClearAsync() { _token = null; _status = new AuthStatus(false); return Task.CompletedTask; }
}

public class SessionStateTests
{
    [Fact]
    public void SessionState_HasDefaultValues()
    {
        var state = new SessionState();

        state.SessionId.Should().NotBeNullOrEmpty();
        state.SessionId.Should().HaveLength(8);
        state.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        state.IsAuthenticated.Should().BeFalse();
        state.AuthSource.Should().BeNull();
        state.Username.Should().BeNull();
        state.CommandCount.Should().Be(0);
        state.ConversationHistory.Should().BeEmpty();
        state.Preferences.Should().BeEmpty();
    }

    [Fact]
    public void SessionState_SessionIdIsUnique()
    {
        var state1 = new SessionState();
        var state2 = new SessionState();

        state1.SessionId.Should().NotBe(state2.SessionId);
    }
}

public class SessionStateServiceTests
{
    [Fact]
    public async Task InitializeAsync_CreatesNewSession()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        var oldSessionId = service.CurrentState.SessionId;

        await service.InitializeAsync();

        service.CurrentState.SessionId.Should().NotBe(oldSessionId);
    }

    [Fact]
    public async Task InitializeAsync_RefreshesAuthStatus()
    {
        var authService = new MockAuthService(true, "testuser", "test source");
        var service = new SessionStateService(authService);

        await service.InitializeAsync();

        service.CurrentState.IsAuthenticated.Should().BeTrue();
        service.CurrentState.Username.Should().Be("testuser");
        service.CurrentState.AuthSource.Should().Be("test source");
    }

    [Fact]
    public async Task RefreshAuthStatusAsync_UpdatesState()
    {
        var authService = new MockAuthService(false);
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        authService.SetStatus(true, "newuser", "env var");
        await service.RefreshAuthStatusAsync();

        service.CurrentState.IsAuthenticated.Should().BeTrue();
        service.CurrentState.Username.Should().Be("newuser");
        service.CurrentState.AuthSource.Should().Be("env var");
    }

    [Fact]
    public async Task RecordCommand_IncrementsCount()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.RecordCommand("version");
        service.RecordCommand("help");
        service.RecordCommand("auth status");

        service.CurrentState.CommandCount.Should().Be(3);
    }

    [Fact]
    public async Task RecordCommand_IgnoresEmptyCommands()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.RecordCommand("");
        service.RecordCommand("   ");
        service.RecordCommand(null!);

        service.CurrentState.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task AddConversationEntry_AddsToHistory()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.AddConversationEntry("msg-001");
        service.AddConversationEntry("msg-002");

        service.CurrentState.ConversationHistory.Should().HaveCount(2);
        service.CurrentState.ConversationHistory.Should().Contain("msg-001");
        service.CurrentState.ConversationHistory.Should().Contain("msg-002");
    }

    [Fact]
    public async Task AddConversationEntry_IgnoresEmptyEntries()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.AddConversationEntry("");
        service.AddConversationEntry("   ");

        service.CurrentState.ConversationHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task SetPreference_StoresValue()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.SetPreference("theme", "dark");

        service.CurrentState.Preferences.Should().ContainKey("theme");
        service.CurrentState.Preferences["theme"].Should().Be("dark");
    }

    [Fact]
    public async Task SetPreference_OverwritesExisting()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.SetPreference("theme", "light");
        service.SetPreference("theme", "dark");

        service.GetPreference("theme").Should().Be("dark");
    }

    [Fact]
    public async Task GetPreference_ReturnsNullIfNotFound()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.GetPreference("nonexistent").Should().BeNull();
    }

    [Fact]
    public async Task GetPreference_ReturnsStoredValue()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.SetPreference("output_format", "json");

        service.GetPreference("output_format").Should().Be("json");
    }

    [Fact]
    public async Task ResetAsync_CreatesNewSession()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);
        await service.InitializeAsync();

        service.RecordCommand("test");
        service.SetPreference("key", "value");
        var oldSessionId = service.CurrentState.SessionId;

        await service.ResetAsync();

        service.CurrentState.SessionId.Should().NotBe(oldSessionId);
        service.CurrentState.CommandCount.Should().Be(0);
        service.CurrentState.Preferences.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ThrowsOnNullAuthService()
    {
        var act = () => new SessionStateService(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetPreference_ThrowsOnNullKey()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);

        var act = () => service.SetPreference(null!, "value");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetPreference_ThrowsOnNullKey()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);

        var act = () => service.GetPreference(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
