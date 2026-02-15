using Lopen.Llm;

namespace Lopen.Core.Workflow;

/// <summary>
/// Orchestrates the 7-step workflow for a module.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Gets the current workflow step for the given module.
    /// </summary>
    WorkflowStep CurrentStep { get; }

    /// <summary>
    /// Initializes the workflow engine for a module by assessing current state.
    /// </summary>
    /// <param name="moduleName">Module to work on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(string moduleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fires a trigger to advance the workflow to the next step.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <returns>True if the transition was valid and executed.</returns>
    bool Fire(WorkflowTrigger trigger);

    /// <summary>
    /// Returns the triggers that are currently permitted.
    /// </summary>
    IReadOnlyList<WorkflowTrigger> GetPermittedTriggers();

    /// <summary>
    /// Returns true if the workflow has reached completion (all components done).
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// Maps the current workflow step to its workflow phase.
    /// </summary>
    WorkflowPhase CurrentPhase { get; }
}
