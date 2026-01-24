namespace Lopen.Core;

/// <summary>
/// Service for interacting with GitHub Copilot.
/// </summary>
public interface ICopilotService : IAsyncDisposable
{
    /// <summary>
    /// Whether Copilot CLI is available and authenticated.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Get authentication status.
    /// </summary>
    Task<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Get available models.
    /// </summary>
    Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Create a new chat session.
    /// </summary>
    Task<ICopilotSession> CreateSessionAsync(CopilotSessionOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Resume an existing session by ID.
    /// </summary>
    Task<ICopilotSession> ResumeSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// List available sessions.
    /// </summary>
    Task<IReadOnlyList<CopilotSessionInfo>> ListSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Delete a session.
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}
