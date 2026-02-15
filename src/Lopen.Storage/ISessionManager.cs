namespace Lopen.Storage;

/// <summary>
/// Manages session lifecycle: creation, persistence, resume, and listing.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Creates a new session for the given module, generating a unique session ID.
    /// </summary>
    Task<SessionId> CreateSessionAsync(string module, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest session ID from the 'latest' symlink, or null if none exists.
    /// </summary>
    Task<SessionId?> GetLatestSessionIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the session state for the given session ID.
    /// </summary>
    Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the session state (atomic write via temp file + rename).
    /// </summary>
    Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the session metrics for the given session ID.
    /// </summary>
    Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the session metrics.
    /// </summary>
    Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all session IDs found in the sessions directory.
    /// </summary>
    Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the 'latest' symlink to point to the given session.
    /// </summary>
    Task SetLatestAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}
