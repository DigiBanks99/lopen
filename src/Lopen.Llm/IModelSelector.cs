namespace Lopen.Llm;

/// <summary>
/// Selects the appropriate model for a workflow phase, with fallback support.
/// </summary>
public interface IModelSelector
{
    /// <summary>
    /// Returns the model to use for the given workflow phase.
    /// Falls back to next available model if configured model is unavailable.
    /// </summary>
    ModelFallbackResult SelectModel(WorkflowPhase phase);

    /// <summary>
    /// Returns the ordered fallback chain for a workflow phase (LLM-11).
    /// Includes the primary model, per-phase fallbacks, and the global fallback.
    /// </summary>
    IReadOnlyList<string> GetFallbackChain(WorkflowPhase phase);
}
