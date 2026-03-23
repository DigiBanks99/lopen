namespace Lopen.Core.Workflow;

/// <summary>
/// Point-in-time snapshot of a module's workflow state, derived from actual codebase truth.
/// </summary>
public sealed record WorkflowAssessment(
    WorkflowStep Step,
    bool IsSpecReady,
    bool HasMoreComponents);
