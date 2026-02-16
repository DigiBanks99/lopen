using Lopen.Core.Workflow;

namespace Lopen.Cli.Tests.Fakes;

/// <summary>
/// Fake orchestrator for CLI tests that completes immediately.
/// </summary>
internal sealed class FakeWorkflowOrchestrator : IWorkflowOrchestrator
{
    public OrchestrationResult? LastResult { get; private set; }
    public string? LastModule { get; private set; }

    public Task<OrchestrationResult> RunAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        LastModule = moduleName;
        LastResult = OrchestrationResult.Completed(1, WorkflowStep.DraftSpecification, "Completed");
        return Task.FromResult(LastResult);
    }

    public Task<StepResult> RunStepAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        LastModule = moduleName;
        return Task.FromResult(StepResult.Succeeded(WorkflowTrigger.Assess, "Step complete"));
    }
}
