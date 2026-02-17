namespace Lopen.Core.Workflow;

/// <summary>
/// Presents a list of modules with their current state and allows the user to select one.
/// Works in both interactive (TUI) and headless modes via IOutputRenderer.
/// </summary>
public interface IModuleSelectionService
{
    /// <summary>
    /// Lists available modules and prompts the user to select one.
    /// Returns the selected module name, or null if no modules found or user cancels.
    /// </summary>
    Task<string?> SelectModuleAsync(CancellationToken cancellationToken = default);
}
