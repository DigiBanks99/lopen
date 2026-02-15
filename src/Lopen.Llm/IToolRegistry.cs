namespace Lopen.Llm;

/// <summary>
/// Manages Lopen-managed tool definitions and provides phase-appropriate tool sets.
/// </summary>
public interface IToolRegistry
{
    /// <summary>Returns tools available for the given workflow phase.</summary>
    IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase);

    /// <summary>Registers a tool definition.</summary>
    void RegisterTool(LopenToolDefinition tool);

    /// <summary>Returns all registered tool definitions.</summary>
    IReadOnlyList<LopenToolDefinition> GetAllTools();
}
