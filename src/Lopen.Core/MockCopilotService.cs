namespace Lopen.Core;

/// <summary>
/// Mock Copilot service for testing.
/// </summary>
public class MockCopilotService : ICopilotService
{
    private readonly Dictionary<string, MockCopilotSession> _sessions = new();
    private readonly List<string> _models = ["gpt-5", "gpt-5.1", "claude-sonnet-4.5"];
    private int _sessionCounter;
    private bool _disposed;
    private string? _configuredResponse;

    /// <summary>
    /// Whether the mock is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Authentication status to return.
    /// </summary>
    public CopilotAuthStatus AuthStatus { get; set; } = new(true, "user", "testuser");

    /// <summary>
    /// Custom session factory.
    /// </summary>
    public Func<CopilotSessionOptions?, MockCopilotSession>? SessionFactory { get; set; }

    /// <summary>
    /// Whether DisposeAsync was called.
    /// </summary>
    public bool WasDisposed => _disposed;

    /// <summary>
    /// Number of sessions created.
    /// </summary>
    public int SessionsCreated => _sessionCounter;

    /// <summary>
    /// Set the response that all sessions will return.
    /// </summary>
    public void SetResponse(string? response)
    {
        _configuredResponse = response;
    }

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(IsAvailable);
    }

    /// <inheritdoc />
    public Task<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken ct = default)
    {
        return Task.FromResult(AuthStatus);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_models);
    }

    /// <inheritdoc />
    public Task<ICopilotSession> CreateSessionAsync(
        CopilotSessionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Copilot not available");

        _sessionCounter++;
        var sessionId = options?.SessionId ?? $"mock-session-{_sessionCounter}";

        MockCopilotSession session;
        if (SessionFactory != null)
        {
            session = SessionFactory(options);
        }
        else if (_configuredResponse != null)
        {
            // Create session with configured response
            var response = _configuredResponse;
            session = new MockCopilotSession(
                sessionId,
                streamHandler: null,
                sendHandler: _ => Task.FromResult<string?>(response));
        }
        else
        {
            session = new MockCopilotSession(sessionId);
        }

        _sessions[sessionId] = session;
        return Task.FromResult<ICopilotSession>(session);
    }

    /// <inheritdoc />
    public Task<ICopilotSession> ResumeSessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));

        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException($"Session not found: {sessionId}");

        return Task.FromResult<ICopilotSession>(session);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CopilotSessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var result = _sessions.Keys
            .Select(id => new CopilotSessionInfo(id, now.AddMinutes(-10), now, null))
            .ToList();
        return Task.FromResult<IReadOnlyList<CopilotSessionInfo>>(result);
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));

        _sessions.Remove(sessionId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
