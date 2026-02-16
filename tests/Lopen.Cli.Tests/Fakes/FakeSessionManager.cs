using Lopen.Storage;

namespace Lopen.Cli.Tests.Fakes;

internal sealed class FakeSessionManager : ISessionManager
{
    private readonly List<SessionId> _sessions = [];
    private readonly Dictionary<string, SessionState> _states = new();
    private readonly Dictionary<string, SessionMetrics> _metrics = new();
    private SessionId? _latestSessionId;

    public bool DeleteCalled { get; private set; }
    public SessionId? LastDeletedSessionId { get; private set; }
    public bool SetLatestCalled { get; private set; }
    public SessionId? LastSetLatestSessionId { get; private set; }

    public Exception? ListException { get; set; }
    public Exception? ShowException { get; set; }
    public Exception? ResumeException { get; set; }
    public Exception? DeleteException { get; set; }
    public Exception? PruneException { get; set; }
    public int PruneResult { get; set; }

    public void AddSession(SessionId id, SessionState state, SessionMetrics? metrics = null)
    {
        _sessions.Add(id);
        _states[id.ToString()] = state;
        if (metrics is not null)
            _metrics[id.ToString()] = metrics;
    }

    public void SetLatestSessionId(SessionId? id) => _latestSessionId = id;

    public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
        => Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));

    public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default)
        => Task.FromResult(_latestSessionId);

    public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken ct = default)
    {
        if (ShowException is not null)
            throw ShowException;
        _states.TryGetValue(sessionId.ToString(), out var state);
        return Task.FromResult(state);
    }

    public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken ct = default)
    {
        _states[sessionId.ToString()] = state;
        return Task.CompletedTask;
    }

    public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken ct = default)
    {
        _metrics.TryGetValue(sessionId.ToString(), out var metrics);
        return Task.FromResult(metrics);
    }

    public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken ct = default)
    {
        _metrics[sessionId.ToString()] = metrics;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default)
    {
        if (ListException is not null)
            throw ListException;
        return Task.FromResult<IReadOnlyList<SessionId>>(_sessions.ToList());
    }

    public Task SetLatestAsync(SessionId sessionId, CancellationToken ct = default)
    {
        if (ResumeException is not null)
            throw ResumeException;
        SetLatestCalled = true;
        LastSetLatestSessionId = sessionId;
        _latestSessionId = sessionId;
        return Task.CompletedTask;
    }

    public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default)
    {
        if (PruneException is not null)
            throw PruneException;
        return Task.FromResult(PruneResult);
    }

    public Task DeleteSessionAsync(SessionId sessionId, CancellationToken ct = default)
    {
        if (DeleteException is not null)
            throw DeleteException;
        DeleteCalled = true;
        LastDeletedSessionId = sessionId;
        _sessions.RemoveAll(s => s.Equals(sessionId));
        _states.Remove(sessionId.ToString());
        _metrics.Remove(sessionId.ToString());
        return Task.CompletedTask;
    }
}
