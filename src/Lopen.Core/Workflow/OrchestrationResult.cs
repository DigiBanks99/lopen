namespace Lopen.Core.Workflow;

/// <summary>
/// Result of a full orchestration run.
/// </summary>
public sealed record OrchestrationResult
{
    /// <summary>Whether the module completed successfully.</summary>
    public required bool IsComplete { get; init; }

    /// <summary>Total iterations executed.</summary>
    public required int IterationCount { get; init; }

    /// <summary>The final workflow step when orchestration ended.</summary>
    public required WorkflowStep FinalStep { get; init; }

    /// <summary>Human-readable summary.</summary>
    public string? Summary { get; init; }

    /// <summary>Whether orchestration was interrupted (by user, guardrail, or cancellation).</summary>
    public bool WasInterrupted { get; init; }

    /// <summary>The reason for interruption, if applicable.</summary>
    public string? InterruptionReason { get; init; }

    /// <summary>Whether the interruption was caused by a critical system error (CORE-23).</summary>
    public bool IsCriticalError { get; init; }

    public static OrchestrationResult Completed(int iterations, WorkflowStep finalStep, string? summary = null) =>
        new()
        {
            IsComplete = true,
            IterationCount = iterations,
            FinalStep = finalStep,
            Summary = summary ?? "Module completed successfully"
        };

    public static OrchestrationResult Interrupted(int iterations, WorkflowStep finalStep, string reason) =>
        new()
        {
            IsComplete = false,
            IterationCount = iterations,
            FinalStep = finalStep,
            WasInterrupted = true,
            InterruptionReason = reason,
            Summary = $"Orchestration interrupted: {reason}"
        };

    /// <summary>
    /// Creates an interrupted result for a critical system error that blocked execution (CORE-23).
    /// </summary>
    public static OrchestrationResult CriticalError(int iterations, WorkflowStep finalStep, string reason) =>
        new()
        {
            IsComplete = false,
            IterationCount = iterations,
            FinalStep = finalStep,
            WasInterrupted = true,
            IsCriticalError = true,
            InterruptionReason = reason,
            Summary = $"CRITICAL ERROR — execution blocked: {reason}"
        };
}
