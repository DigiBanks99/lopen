namespace Lopen.Core.Workflow;

/// <summary>
/// Result of a single workflow step execution.
/// </summary>
public sealed record StepResult
{
    /// <summary>Whether the step completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>The trigger to fire for the next transition, if any.</summary>
    public WorkflowTrigger? NextTrigger { get; init; }

    /// <summary>Human-readable summary of what happened.</summary>
    public string? Summary { get; init; }

    /// <summary>Whether user confirmation is needed before proceeding.</summary>
    public bool RequiresUserConfirmation { get; init; }

    public static StepResult Succeeded(WorkflowTrigger nextTrigger, string? summary = null) =>
        new() { Success = true, NextTrigger = nextTrigger, Summary = summary };

    public static StepResult Failed(string summary) =>
        new() { Success = false, Summary = summary };

    public static StepResult NeedsConfirmation(string summary) =>
        new() { Success = true, RequiresUserConfirmation = true, Summary = summary };

    public static StepResult Completed(string? summary = null) =>
        new() { Success = true, Summary = summary ?? "Module complete" };
}
