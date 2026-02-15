using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Lopen.Storage;

/// <summary>
/// File-system-backed session manager.
/// </summary>
internal sealed class SessionManager : ISessionManager
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SessionManager> _logger;
    private readonly string _projectRoot;

    internal static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    public SessionManager(IFileSystem fileSystem, ILogger<SessionManager> logger, string projectRoot)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        _projectRoot = projectRoot;
    }

    public async Task<SessionId> CreateSessionAsync(string module, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counter = await GetNextCounterAsync(module, today, cancellationToken);
        var sessionId = SessionId.Generate(module, today, counter);

        var sessionDir = StoragePaths.GetSessionDirectory(_projectRoot, sessionId);
        _fileSystem.CreateDirectory(sessionDir);

        var now = DateTimeOffset.UtcNow;
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "req-gathering",
            Step = "draft-spec",
            Module = module,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await SaveSessionStateAsync(sessionId, state, cancellationToken);
        await SetLatestAsync(sessionId, cancellationToken);

        _logger.LogInformation("Created session {SessionId}", sessionId);
        return sessionId;
    }

    public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken cancellationToken = default)
    {
        var latestPath = StoragePaths.GetLatestSymlinkPath(_projectRoot);

        if (!_fileSystem.FileExists(latestPath) && !_fileSystem.DirectoryExists(latestPath))
        {
            return Task.FromResult<SessionId?>(null);
        }

        var target = _fileSystem.GetSymlinkTarget(latestPath);
        if (string.IsNullOrWhiteSpace(target))
        {
            return Task.FromResult<SessionId?>(null);
        }

        var dirName = Path.GetFileName(target);
        return Task.FromResult(SessionId.TryParse(dirName));
    }

    public async Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var statePath = StoragePaths.GetSessionStatePath(_projectRoot, sessionId);

        if (!_fileSystem.FileExists(statePath))
        {
            return null;
        }

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(statePath, cancellationToken);
            return JsonSerializer.Deserialize<SessionState>(json, CompactJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted session state at {Path}", statePath);
            throw new StorageException($"Corrupted session state: {statePath}", statePath, ex);
        }
    }

    public async Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(state);

        var statePath = StoragePaths.GetSessionStatePath(_projectRoot, sessionId);
        var sessionDir = Path.GetDirectoryName(statePath)!;
        _fileSystem.CreateDirectory(sessionDir);

        var json = JsonSerializer.Serialize(state, CompactJsonOptions);

        // Atomic write: write to temp file then move
        var tempPath = statePath + ".tmp";
        await _fileSystem.WriteAllTextAsync(tempPath, json, cancellationToken);
        _fileSystem.MoveFile(tempPath, statePath);

        _logger.LogDebug("Saved session state for {SessionId}", sessionId);
    }

    public async Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var metricsPath = StoragePaths.GetSessionMetricsPath(_projectRoot, sessionId);

        if (!_fileSystem.FileExists(metricsPath))
        {
            return null;
        }

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(metricsPath, cancellationToken);
            return JsonSerializer.Deserialize<SessionMetrics>(json, CompactJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted session metrics at {Path}", metricsPath);
            throw new StorageException($"Corrupted session metrics: {metricsPath}", metricsPath, ex);
        }
    }

    public async Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(metrics);

        var metricsPath = StoragePaths.GetSessionMetricsPath(_projectRoot, sessionId);
        var sessionDir = Path.GetDirectoryName(metricsPath)!;
        _fileSystem.CreateDirectory(sessionDir);

        var json = JsonSerializer.Serialize(metrics, CompactJsonOptions);

        var tempPath = metricsPath + ".tmp";
        await _fileSystem.WriteAllTextAsync(tempPath, json, cancellationToken);
        _fileSystem.MoveFile(tempPath, metricsPath);

        _logger.LogDebug("Saved session metrics for {SessionId}", sessionId);
    }

    public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessionsDir = StoragePaths.GetSessionsDirectory(_projectRoot);

        if (!_fileSystem.DirectoryExists(sessionsDir))
        {
            return Task.FromResult<IReadOnlyList<SessionId>>(Array.Empty<SessionId>());
        }

        var sessions = _fileSystem.GetDirectories(sessionsDir)
            .Select(d => SessionId.TryParse(Path.GetFileName(d)))
            .Where(id => id is not null)
            .Cast<SessionId>()
            .OrderBy(id => id.Date)
            .ThenBy(id => id.Counter)
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionId>>(sessions);
    }

    public Task SetLatestAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var latestPath = StoragePaths.GetLatestSymlinkPath(_projectRoot);
        var sessionDir = StoragePaths.GetSessionDirectory(_projectRoot, sessionId);

        _fileSystem.CreateDirectory(StoragePaths.GetSessionsDirectory(_projectRoot));

        // Remove existing symlink if present
        if (_fileSystem.FileExists(latestPath) || _fileSystem.DirectoryExists(latestPath))
        {
            _fileSystem.DeleteFile(latestPath);
        }

        _fileSystem.CreateSymlink(latestPath, sessionDir);
        _logger.LogDebug("Set latest symlink to {SessionId}", sessionId);

        return Task.CompletedTask;
    }

    private Task<int> GetNextCounterAsync(string module, DateOnly date, CancellationToken cancellationToken)
    {
        var sessionsDir = StoragePaths.GetSessionsDirectory(_projectRoot);

        if (!_fileSystem.DirectoryExists(sessionsDir))
        {
            return Task.FromResult(1);
        }

        var prefix = $"{module.ToLowerInvariant()}-{date:yyyyMMdd}-";
        var existingCounters = _fileSystem.GetDirectories(sessionsDir)
            .Select(d => Path.GetFileName(d))
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => SessionId.TryParse(name))
            .Where(id => id is not null)
            .Select(id => id!.Counter)
            .ToList();

        var nextCounter = existingCounters.Count > 0 ? existingCounters.Max() + 1 : 1;
        return Task.FromResult(nextCounter);
    }
}
