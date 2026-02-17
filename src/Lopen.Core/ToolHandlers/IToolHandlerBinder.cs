using Lopen.Llm;

namespace Lopen.Core.ToolHandlers;

/// <summary>
/// Binds tool handler implementations to the tool registry.
/// Called after DI setup to connect handlers to tool definitions.
/// </summary>
public interface IToolHandlerBinder
{
    /// <summary>
    /// Binds all Lopen tool handlers to their corresponding tool definitions in the registry.
    /// </summary>
    void BindAll(IToolRegistry registry);
}
