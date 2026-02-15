using Lopen.Core.Workflow;

namespace Lopen.Core.Workflow;

/// <summary>
/// Assesses the current state of the codebase and determines the correct workflow step.
/// </summary>
public interface IStateAssessor
{
    /// <summary>
    /// Determines the current workflow step by assessing actual codebase state.
    /// </summary>
    /// <param name="moduleName">The module being worked on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current workflow step.</returns>
    Task<WorkflowStep> GetCurrentStepAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the current workflow step for a module.
    /// </summary>
    /// <param name="moduleName">The module being worked on.</param>
    /// <param name="step">The step to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PersistStepAsync(string moduleName, WorkflowStep step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specification for a module is ready (exists and is parseable).
    /// </summary>
    /// <param name="moduleName">The module to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the specification is ready.</returns>
    Task<bool> IsSpecReadyAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether there are more components to build for a module.
    /// </summary>
    /// <param name="moduleName">The module to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if there are incomplete components remaining.</returns>
    Task<bool> HasMoreComponentsAsync(string moduleName, CancellationToken cancellationToken = default);
}
