using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

public sealed class AutoSaveServiceTests
{
    private static readonly SessionId TestSessionId = SessionId.Generate("test", DateOnly.FromDateTime(DateTime.UtcNow), 1);

    private static SessionState CreateState(string module = "auth") => new()
    {
        SessionId = TestSessionId.ToString(),
        Phase = "building",
        Step = "iterate",
        Module = module,
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
    };

    private static AutoSaveService CreateService(ISessionManager? manager = null)
    {
        return new AutoSaveService(
            manager ?? new FakeSessionManager(),
            NullLogger<AutoSaveService>.Instance);
    }

    // --- Constructor null guards ---

    [Fact]
    public void Constructor_NullSessionManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AutoSaveService(null!, NullLogger<AutoSaveService>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AutoSaveService(new FakeSessionManager(), null!));
    }

    // --- SaveAsync argument validation ---

    [Fact]
    public async Task SaveAsync_NullSessionId_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SaveAsync(AutoSaveTrigger.TaskCompletion, null!, CreateState()));
    }

    [Fact]
    public async Task SaveAsync_NullState_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SaveAsync(AutoSaveTrigger.TaskCompletion, TestSessionId, null!));
    }

    // --- SaveAsync saves state ---

    [Fact]
    public async Task SaveAsync_SavesSessionState()
    {
        var manager = new FakeSessionManager();
        var service = CreateService(manager);
        var state = CreateState();

        await service.SaveAsync(AutoSaveTrigger.TaskCompletion, TestSessionId, state);

        Assert.NotNull(manager.LastSavedState);
        Assert.Equal(TestSessionId, manager.LastSavedSessionId);
    }

    [Fact]
    public async Task SaveAsync_UpdatesTimestamp()
    {
        var manager = new FakeSessionManager();
        var service = CreateService(manager);
        var state = CreateState();
        var before = DateTimeOffset.UtcNow;

        await service.SaveAsync(AutoSaveTrigger.StepCompletion, TestSessionId, state);

        Assert.NotNull(manager.LastSavedState);
        Assert.True(manager.LastSavedState!.UpdatedAt >= before);
    }

    [Theory]
    [InlineData(AutoSaveTrigger.StepCompletion)]
    [InlineData(AutoSaveTrigger.TaskCompletion)]
    [InlineData(AutoSaveTrigger.TaskFailure)]
    [InlineData(AutoSaveTrigger.PhaseTransition)]
    [InlineData(AutoSaveTrigger.ComponentCompletion)]
    [InlineData(AutoSaveTrigger.UserPause)]
    public async Task SaveAsync_AllTriggers_SaveState(AutoSaveTrigger trigger)
    {
        var manager = new FakeSessionManager();
        var service = CreateService(manager);

        await service.SaveAsync(trigger, TestSessionId, CreateState());

        Assert.NotNull(manager.LastSavedState);
    }

    // --- SaveAsync saves metrics when provided ---

    [Fact]
    public async Task SaveAsync_WithMetrics_SavesBoth()
    {
        var manager = new FakeSessionManager();
        var service = CreateService(manager);
        var metrics = new SessionMetrics
        {
            SessionId = TestSessionId.ToString(),
            CumulativeInputTokens = 1000,
            CumulativeOutputTokens = 500,
            IterationCount = 3,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await service.SaveAsync(AutoSaveTrigger.TaskCompletion, TestSessionId, CreateState(), metrics);

        Assert.NotNull(manager.LastSavedState);
        Assert.NotNull(manager.LastSavedMetrics);
        Assert.Equal(1000, manager.LastSavedMetrics!.CumulativeInputTokens);
    }

    [Fact]
    public async Task SaveAsync_WithoutMetrics_OnlySavesState()
    {
        var manager = new FakeSessionManager();
        var service = CreateService(manager);

        await service.SaveAsync(AutoSaveTrigger.TaskCompletion, TestSessionId, CreateState());

        Assert.NotNull(manager.LastSavedState);
        Assert.Null(manager.LastSavedMetrics);
    }

    // --- SaveAsync error handling ---

    [Fact]
    public async Task SaveAsync_StorageException_DoesNotThrow()
    {
        var manager = new ThrowingSessionManager();
        var service = CreateService(manager);

        // Should not throw - auto-save failures should not crash the workflow
        await service.SaveAsync(AutoSaveTrigger.TaskCompletion, TestSessionId, CreateState());
    }

    [Fact]
    public async Task SaveAsync_CriticalStorageException_Throws()
    {
        var manager = new CriticalThrowingSessionManager();
        var service = CreateService(manager);

        // STOR-16: Critical write failures must propagate
        await Assert.ThrowsAsync<StorageException>(() =>
            service.SaveAsync(AutoSaveTrigger.TaskCompletion, TestSessionId, CreateState()));
    }

    // --- Test doubles ---

    internal sealed class FakeSessionManager : ISessionManager
    {
        public SessionId? LastSavedSessionId { get; private set; }
        public SessionState? LastSavedState { get; private set; }
        public SessionMetrics? LastSavedMetrics { get; private set; }

        public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
            => Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));

        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default)
            => Task.FromResult<SessionId?>(null);

        public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.FromResult<SessionState?>(null);

        public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken ct = default)
        {
            LastSavedSessionId = sessionId;
            LastSavedState = state;
            return Task.CompletedTask;
        }

        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.FromResult<SessionMetrics?>(null);

        public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken ct = default)
        {
            LastSavedMetrics = metrics;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionId>>(Array.Empty<SessionId>());

        public Task SetLatestAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task DeleteSessionAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingSessionManager : ISessionManager
    {
        public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
            => Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));
        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default) => Task.FromResult<SessionId?>(null);
        public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken ct = default) => Task.FromResult<SessionState?>(null);
        public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken ct = default)
            => throw new StorageException("Disk full");
        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken ct = default) => Task.FromResult<SessionMetrics?>(null);
        public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SessionId>>(Array.Empty<SessionId>());
        public Task SetLatestAsync(SessionId sessionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default) => Task.FromResult(0);
        public Task DeleteSessionAsync(SessionId sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CriticalThrowingSessionManager : ISessionManager
    {
        public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
            => Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));
        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default) => Task.FromResult<SessionId?>(null);
        public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken ct = default) => Task.FromResult<SessionState?>(null);
        public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken ct = default)
            => throw new StorageException("Disk full", "/tmp/session.json", new IOException("No space left on device"));
        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken ct = default) => Task.FromResult<SessionMetrics?>(null);
        public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SessionId>>(Array.Empty<SessionId>());
        public Task SetLatestAsync(SessionId sessionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default) => Task.FromResult(0);
        public Task DeleteSessionAsync(SessionId sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
