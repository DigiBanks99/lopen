namespace Lopen.Core.Workflow;

/// <summary>
/// Lists modules with their current development state for selection.
/// </summary>
public interface IModuleLister
{
    /// <summary>
    /// Scans modules and determines their state from acceptance criteria checkboxes.
    /// </summary>
    /// <returns>Modules with state information, sorted alphabetically.</returns>
    IReadOnlyList<ModuleState> ListModules();
}
