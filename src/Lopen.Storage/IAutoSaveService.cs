namespace Lopen.Storage;

/// <summary>
/// Automatically saves session state and metrics at workflow boundaries.
/// </summary>
public interface IAutoSaveService
{
    /// <summary>
    /// Saves session state and metrics in response to a workflow event.
    /// No-op if no active session exists.
    /// </summary>
    /// <param name="trigger">The event that triggered the save.</param>
    /// <param name="sessionId">The current session ID.</param>
    /// <param name="state">The current session state.</param>
    /// <param name="metrics">The current session metrics, if available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(
        AutoSaveTrigger trigger,
        SessionId sessionId,
        SessionState state,
        SessionMetrics? metrics = null,
        CancellationToken cancellationToken = default);
}
