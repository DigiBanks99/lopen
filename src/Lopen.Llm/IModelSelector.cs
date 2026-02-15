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
}
