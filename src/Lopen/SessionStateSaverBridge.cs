using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen;

/// <summary>
/// Bridges <see cref="ISessionManager"/> to <see cref="ISessionStateSaver"/> so that
/// <c>AuthErrorHandler</c> can persist session state before aborting on non-recoverable auth errors.
/// </summary>
internal sealed class SessionStateSaverBridge : ISessionStateSaver
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionStateSaverBridge> _logger;

    public SessionStateSaverBridge(ISessionManager sessionManager, ILogger<SessionStateSaverBridge> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var sessionId = await _sessionManager.GetLatestSessionIdAsync(cancellationToken);
        if (sessionId is null)
        {
            _logger.LogWarning("No active session found; nothing to save.");
            return;
        }

        var state = await _sessionManager.LoadSessionStateAsync(sessionId, cancellationToken);
        if (state is null)
        {
            _logger.LogWarning("Session {SessionId} has no persisted state; nothing to save.", sessionId);
            return;
        }

        var updated = state with { UpdatedAt = DateTimeOffset.UtcNow };
        await _sessionManager.SaveSessionStateAsync(sessionId, updated, cancellationToken);

        _logger.LogInformation("Session state saved for {SessionId}.", sessionId);
    }
}
