using Lopen.Cli.Tests.Fakes;
using Lopen.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Cli.Tests;

public class SessionStateSaverBridgeTests
{
    private static readonly SessionId TestSessionId =
        SessionId.Generate("test", DateOnly.FromDateTime(DateTime.UtcNow), 1);

    private static SessionState CreateTestState() => new()
    {
        SessionId = TestSessionId.ToString(),
        Phase = "building",
        Step = "coding",
        Module = "test",
        CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
    };

    [Fact]
    public void Constructor_NullSessionManager_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionStateSaverBridge(null!, NullLogger<SessionStateSaverBridge>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        var manager = new FakeSessionManager();
        Assert.Throws<ArgumentNullException>(() =>
            new SessionStateSaverBridge(manager, null!));
    }

    [Fact]
    public async Task SaveAsync_NoActiveSession_ReturnsWithoutSaving()
    {
        var manager = new FakeSessionManager();
        // No latest session set — GetLatestSessionIdAsync returns null.
        var saver = new SessionStateSaverBridge(manager, NullLogger<SessionStateSaverBridge>.Instance);

        await saver.SaveAsync();

        // No session was added, so no state could have been saved.
        var sessions = await manager.ListSessionsAsync();
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task SaveAsync_NoPersistedState_ReturnsWithoutSaving()
    {
        var manager = new FakeSessionManager();
        // Set a latest session but don't add any state for it.
        manager.SetLatestSessionId(TestSessionId);
        var saver = new SessionStateSaverBridge(manager, NullLogger<SessionStateSaverBridge>.Instance);

        await saver.SaveAsync();

        // LoadSessionStateAsync returns null so SaveSessionStateAsync is never called.
        var state = await manager.LoadSessionStateAsync(TestSessionId);
        Assert.Null(state);
    }

    [Fact]
    public async Task SaveAsync_PersistsUpdatedState()
    {
        var manager = new FakeSessionManager();
        var originalState = CreateTestState();
        manager.AddSession(TestSessionId, originalState);
        manager.SetLatestSessionId(TestSessionId);

        var saver = new SessionStateSaverBridge(manager, NullLogger<SessionStateSaverBridge>.Instance);
        var before = DateTimeOffset.UtcNow;

        await saver.SaveAsync();

        var saved = await manager.LoadSessionStateAsync(TestSessionId);
        Assert.NotNull(saved);
        Assert.True(saved!.UpdatedAt >= before, "UpdatedAt should be refreshed to a recent timestamp.");
        Assert.True(saved.UpdatedAt > originalState.UpdatedAt, "UpdatedAt should be later than original.");
    }
}
