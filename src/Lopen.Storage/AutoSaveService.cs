using Microsoft.Extensions.Logging;

namespace Lopen.Storage;

/// <summary>
/// Automatically saves session state and metrics at workflow boundaries.
/// </summary>
internal sealed class AutoSaveService : IAutoSaveService
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<AutoSaveService> _logger;

    public AutoSaveService(ISessionManager sessionManager, ILogger<AutoSaveService> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveAsync(
        AutoSaveTrigger trigger,
        SessionId sessionId,
        SessionState state,
        SessionMetrics? metrics = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(state);

        _logger.LogInformation(
            "Auto-saving session {SessionId} on {Trigger}", sessionId, trigger);

        try
        {
            var updatedState = state with { UpdatedAt = DateTimeOffset.UtcNow };
            await _sessionManager.SaveSessionStateAsync(sessionId, updatedState, cancellationToken);

            if (metrics is not null)
            {
                await _sessionManager.SaveSessionMetricsAsync(sessionId, metrics, cancellationToken);
            }

            _logger.LogDebug(
                "Auto-save complete for session {SessionId} on {Trigger}",
                sessionId, trigger);
        }
        catch (StorageException ex) when (ex.IsCritical)
        {
            _logger.LogCritical(ex,
                "Critical write failure during auto-save for session {SessionId} on {Trigger} — path: {Path}",
                sessionId, trigger, ex.Path);
            throw; // STOR-16: Critical storage errors must propagate to pause the workflow
        }
        catch (StorageException ex)
        {
            _logger.LogError(ex,
                "Auto-save failed for session {SessionId} on {Trigger}",
                sessionId, trigger);
            // Non-critical auto-save failures should not crash the workflow (STOR-06)
        }
    }
}
