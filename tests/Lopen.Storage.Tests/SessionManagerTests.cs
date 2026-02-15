using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

public class SessionManagerTests
{
    private readonly InMemoryFileSystem _fileSystem;
    private readonly ILogger<SessionManager> _logger;
    private readonly string _projectRoot;
    private readonly SessionManager _manager;

    public SessionManagerTests()
    {
        _fileSystem = new InMemoryFileSystem();
        _logger = NullLogger<SessionManager>.Instance;
        _projectRoot = "/test/project";
        _manager = new SessionManager(_fileSystem, _logger, _projectRoot);
    }

    [Fact]
    public void Constructor_ThrowsOnNullFileSystem()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionManager(null!, _logger, _projectRoot));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionManager(_fileSystem, null!, _projectRoot));
    }

    [Fact]
    public void Constructor_ThrowsOnNullProjectRoot()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SessionManager(_fileSystem, _logger, null!));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyProjectRoot()
    {
        Assert.Throws<ArgumentException>(() =>
            new SessionManager(_fileSystem, _logger, ""));
    }

    [Fact]
    public async Task CreateSessionAsync_ReturnsSessionIdWithCorrectModule()
    {
        var sessionId = await _manager.CreateSessionAsync("auth");

        Assert.Equal("auth", sessionId.Module);
        Assert.Equal(1, sessionId.Counter);
    }

    [Fact]
    public async Task CreateSessionAsync_CreatesSessionDirectory()
    {
        var sessionId = await _manager.CreateSessionAsync("auth");

        var sessionDir = StoragePaths.GetSessionDirectory(_projectRoot, sessionId);
        Assert.True(_fileSystem.DirectoryExists(sessionDir));
    }

    [Fact]
    public async Task CreateSessionAsync_SavesInitialState()
    {
        var sessionId = await _manager.CreateSessionAsync("auth");

        var state = await _manager.LoadSessionStateAsync(sessionId);
        Assert.NotNull(state);
        Assert.Equal(sessionId.ToString(), state.SessionId);
        Assert.Equal("auth", state.Module);
        Assert.Equal("req-gathering", state.Phase);
        Assert.Equal("draft-spec", state.Step);
    }

    [Fact]
    public async Task CreateSessionAsync_SetsLatestSymlink()
    {
        var sessionId = await _manager.CreateSessionAsync("auth");

        var latest = await _manager.GetLatestSessionIdAsync();
        Assert.NotNull(latest);
        Assert.Equal(sessionId, latest);
    }

    [Fact]
    public async Task CreateSessionAsync_IncrementsCounter()
    {
        var first = await _manager.CreateSessionAsync("auth");
        var second = await _manager.CreateSessionAsync("auth");

        Assert.Equal(1, first.Counter);
        Assert.Equal(2, second.Counter);
    }

    [Fact]
    public async Task CreateSessionAsync_ThrowsOnNullModule()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.CreateSessionAsync(null!));
    }

    [Fact]
    public async Task SaveSessionStateAsync_And_LoadSessionStateAsync_RoundTrips()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "implement",
            Module = "auth",
            Component = "login",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsComplete = false,
        };

        await _manager.SaveSessionStateAsync(sessionId, state);
        var loaded = await _manager.LoadSessionStateAsync(sessionId);

        Assert.NotNull(loaded);
        Assert.Equal("building", loaded.Phase);
        Assert.Equal("implement", loaded.Step);
        Assert.Equal("login", loaded.Component);
    }

    [Fact]
    public async Task LoadSessionStateAsync_ReturnsNull_WhenNoStateFile()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 99);

        var result = await _manager.LoadSessionStateAsync(sessionId);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveSessionMetricsAsync_And_LoadSessionMetricsAsync_RoundTrips()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var metrics = new SessionMetrics
        {
            SessionId = sessionId.ToString(),
            CumulativeInputTokens = 1000,
            CumulativeOutputTokens = 500,
            PremiumRequestCount = 3,
            IterationCount = 5,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _manager.SaveSessionMetricsAsync(sessionId, metrics);
        var loaded = await _manager.LoadSessionMetricsAsync(sessionId);

        Assert.NotNull(loaded);
        Assert.Equal(1000, loaded.CumulativeInputTokens);
        Assert.Equal(500, loaded.CumulativeOutputTokens);
        Assert.Equal(3, loaded.PremiumRequestCount);
        Assert.Equal(5, loaded.IterationCount);
    }

    [Fact]
    public async Task LoadSessionMetricsAsync_ReturnsNull_WhenNoMetricsFile()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 99);

        var result = await _manager.LoadSessionMetricsAsync(sessionId);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsEmpty_WhenNoSessions()
    {
        var sessions = await _manager.ListSessionsAsync();

        Assert.Empty(sessions);
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsCreatedSessions()
    {
        await _manager.CreateSessionAsync("auth");
        await _manager.CreateSessionAsync("core");

        var sessions = await _manager.ListSessionsAsync();

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.Module == "auth");
        Assert.Contains(sessions, s => s.Module == "core");
    }

    [Fact]
    public async Task SetLatestAsync_UpdatesSymlink()
    {
        var first = await _manager.CreateSessionAsync("auth");
        var second = await _manager.CreateSessionAsync("core");

        await _manager.SetLatestAsync(first);
        var latest = await _manager.GetLatestSessionIdAsync();

        Assert.Equal(first, latest);
    }

    [Fact]
    public async Task GetLatestSessionIdAsync_ReturnsNull_WhenNoSymlink()
    {
        var result = await _manager.GetLatestSessionIdAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveSessionStateAsync_UsesAtomicWrite()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "planning",
            Step = "dependencies",
            Module = "auth",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _manager.SaveSessionStateAsync(sessionId, state);

        // The temp file should not exist after save
        var statePath = StoragePaths.GetSessionStatePath(_projectRoot, sessionId);
        var tempPath = statePath + ".tmp";
        Assert.False(_fileSystem.FileExists(tempPath));

        // The actual file should exist
        Assert.True(_fileSystem.FileExists(statePath));
    }

    [Fact]
    public async Task SaveSessionStateAsync_ThrowsOnNullSessionId()
    {
        var state = new SessionState
        {
            SessionId = "test",
            Phase = "p",
            Step = "s",
            Module = "m",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.SaveSessionStateAsync(null!, state));
    }

    [Fact]
    public async Task SaveSessionStateAsync_ThrowsOnNullState()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.SaveSessionStateAsync(sessionId, null!));
    }
}
