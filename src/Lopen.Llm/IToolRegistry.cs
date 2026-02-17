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

    /// <summary>Binds a handler to an existing tool definition by name.</summary>
    /// <param name="toolName">Name of the tool to bind the handler to.</param>
    /// <param name="handler">The handler function: takes parameters JSON, returns result string.</param>
    /// <returns>True if the tool was found and handler bound; false otherwise.</returns>
    bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler);
}
