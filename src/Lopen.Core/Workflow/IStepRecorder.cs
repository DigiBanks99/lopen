namespace Lopen.Core.Workflow;

/// <summary>
/// Records workflow step progress to disk so sessions can resume at the correct planning step.
/// Called by WorkflowOrchestrator after every successful planning-phase transition.
/// </summary>
public interface IStepRecorder
{
    /// <summary>
    /// Persists a planning-phase step hint to disk for the given module.
    /// </summary>
    Task RecordStepAsync(string moduleName, WorkflowStep step, CancellationToken cancellationToken = default);
}
