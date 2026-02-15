namespace Lopen.Core.Workflow;

/// <summary>
/// Scans the docs/requirements/ directory for module specifications.
/// </summary>
public interface IModuleScanner
{
    /// <summary>
    /// Scans the requirements directory and returns all discovered modules.
    /// </summary>
    /// <returns>List of discovered module specifications.</returns>
    IReadOnlyList<ModuleInfo> ScanModules();
}
