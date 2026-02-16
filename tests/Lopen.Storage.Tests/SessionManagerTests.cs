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

    // === QuarantineCorruptedSessionAsync ===

    [Fact]
    public async Task QuarantineCorruptedSessionAsync_MovesSessionToCorruptedDir()
    {
        var sessionId = await _manager.CreateSessionAsync("auth");

        await _manager.QuarantineCorruptedSessionAsync(sessionId);

        var corruptedDir = StoragePaths.GetCorruptedDirectory(_projectRoot);
        Assert.True(_fileSystem.DirectoryExists(Path.Combine(corruptedDir, sessionId.ToString())));
    }

    [Fact]
    public async Task QuarantineCorruptedSessionAsync_NonExistentSession_DoesNotThrow()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 99);

        await _manager.QuarantineCorruptedSessionAsync(sessionId);
    }

    [Fact]
    public async Task QuarantineCorruptedSessionAsync_NullSessionId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.QuarantineCorruptedSessionAsync(null!));
    }

    // === PruneSessionsAsync ===

    [Fact]
    public async Task PruneSessionsAsync_FewerThanRetention_PrunesNothing()
    {
        await _manager.CreateSessionAsync("auth");
        await _manager.CreateSessionAsync("auth");

        var pruned = await _manager.PruneSessionsAsync(5);

        Assert.Equal(0, pruned);
        var sessions = await _manager.ListSessionsAsync();
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task PruneSessionsAsync_ExactlyRetention_PrunesNothing()
    {
        await _manager.CreateSessionAsync("auth");
        await _manager.CreateSessionAsync("core");
        await _manager.CreateSessionAsync("llm");

        var pruned = await _manager.PruneSessionsAsync(3);

        Assert.Equal(0, pruned);
    }

    [Fact]
    public async Task PruneSessionsAsync_MoreThanRetention_PrunesOldest()
    {
        await _manager.CreateSessionAsync("auth");
        await _manager.CreateSessionAsync("auth");
        await _manager.CreateSessionAsync("auth");

        var pruned = await _manager.PruneSessionsAsync(1);

        Assert.Equal(2, pruned);
    }

    [Fact]
    public async Task PruneSessionsAsync_InvalidRetention_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _manager.PruneSessionsAsync(0));
    }

    // === DeleteSessionAsync ===

    [Fact]
    public async Task DeleteSessionAsync_ExistingSession_RemovesDirectory()
    {
        var session = await _manager.CreateSessionAsync("auth");
        await _manager.DeleteSessionAsync(session);

        // Session should no longer appear in list
        var sessions = await _manager.ListSessionsAsync();
        Assert.DoesNotContain(sessions, s => s.Equals(session));
    }

    [Fact]
    public async Task DeleteSessionAsync_NonexistentSession_ThrowsStorageException()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 99);

        await Assert.ThrowsAsync<StorageException>(() =>
            _manager.DeleteSessionAsync(sessionId));
    }

    [Fact]
    public async Task DeleteSessionAsync_NullSessionId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _manager.DeleteSessionAsync(null!));
    }

    // === IO Error Wrapping ===

    [Fact]
    public async Task SaveSessionStateAsync_IoError_ThrowsStorageException()
    {
        var failFs = new FailingWriteFileSystem();
        var mgr = new SessionManager(failFs, _logger, _projectRoot);
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "p",
            Step = "s",
            Module = "m",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAsync<StorageException>(() =>
            mgr.SaveSessionStateAsync(sessionId, state));
    }

    [Fact]
    public async Task SaveSessionMetricsAsync_IoError_ThrowsStorageException()
    {
        var failFs = new FailingWriteFileSystem();
        var mgr = new SessionManager(failFs, _logger, _projectRoot);
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var metrics = new SessionMetrics
        {
            SessionId = sessionId.ToString(),
            CumulativeInputTokens = 100,
            CumulativeOutputTokens = 50,
            PremiumRequestCount = 1,
            IterationCount = 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAsync<StorageException>(() =>
            mgr.SaveSessionMetricsAsync(sessionId, metrics));
    }

    /// <summary>
    /// File system that fails on write operations (simulates disk full/write failure).
    /// </summary>
    private sealed class FailingWriteFileSystem : IFileSystem
    {
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

        public void CreateDirectory(string path) => _directories.Add(path);
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => _directories.Contains(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct) => throw new FileNotFoundException();
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct) => throw new IOException("Disk full");
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string src, string dst) => throw new IOException("Disk full");
        public void DeleteFile(string path) { }
        public void DeleteDirectory(string path, bool recursive = true) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.MinValue;
    }
}
