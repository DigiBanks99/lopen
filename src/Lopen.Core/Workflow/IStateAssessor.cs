namespace Lopen.Core.Workflow;

/// <summary>
/// Assesses the current workflow state of a module from actual codebase truth.
/// Re-entrant: always reflects reality, never trusts stale in-process data.
/// </summary>
public interface IStateAssessor
{
    /// <summary>
    /// Derives the current workflow step and related state from actual codebase state
    /// (spec existence, checkbox completion, on-disk step hint).
    /// One I/O round-trip regardless of how many fields the caller inspects.
    /// </summary>
    Task<WorkflowAssessment> AssessAsync(string moduleName, CancellationToken cancellationToken = default);
}
