namespace Lopen.Core.Workflow;

/// <summary>
/// Module information enriched with current development state.
/// </summary>
/// <param name="Name">Module name derived from folder name.</param>
/// <param name="SpecificationPath">Path to the module's SPECIFICATION.md.</param>
/// <param name="Status">Current development state.</param>
/// <param name="CompletedCriteria">Number of checked acceptance criteria.</param>
/// <param name="TotalCriteria">Total number of acceptance criteria.</param>
public sealed record ModuleState(
    string Name,
    string SpecificationPath,
    ModuleStatus Status,
    int CompletedCriteria,
    int TotalCriteria);
