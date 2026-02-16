namespace Lopen.Llm;

/// <summary>
/// Saves the current session state. Used to persist state before aborting
/// due to non-recoverable errors (e.g., revoked tokens).
/// The composition root wires the real implementation.
/// </summary>
public interface ISessionStateSaver
{
    /// <summary>
    /// Saves the current session state.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
