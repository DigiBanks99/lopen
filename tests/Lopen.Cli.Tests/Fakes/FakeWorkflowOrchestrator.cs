using Lopen.Core.Workflow;

namespace Lopen.Cli.Tests.Fakes;

/// <summary>
/// Fake orchestrator for CLI tests that completes immediately.
/// </summary>
internal sealed class FakeWorkflowOrchestrator : IWorkflowOrchestrator
{
    public OrchestrationResult? LastResult { get; private set; }
    public string? LastModule { get; private set; }
    public string? LastPrompt { get; private set; }

    public Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
    {
        LastModule = moduleName;
        LastPrompt = userPrompt;
        LastResult = OrchestrationResult.Completed(1, WorkflowStep.DraftSpecification, "Completed");
        return Task.FromResult(LastResult);
    }

    public Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
    {
        LastModule = moduleName;
        LastPrompt = userPrompt;
        return Task.FromResult(StepResult.Succeeded(WorkflowTrigger.Assess, "Step complete"));
    }
}
