namespace Lopen.Llm;

/// <summary>
/// Validates whether a task status transition to "complete" is allowed.
/// Enforces that update_task_status(complete) is rejected unless preceded
/// by a passing verify_*_completion call in the same invocation.
/// </summary>
public interface ITaskStatusGate
{
    /// <summary>
    /// Validates whether a task can be marked as complete.
    /// </summary>
    /// <param name="scope">The verification scope (Task, Component, or Module).</param>
    /// <param name="identifier">The unique identifier of the item being completed.</param>
    /// <returns>A validation result indicating whether completion is allowed.</returns>
    TaskStatusGateResult ValidateCompletion(VerificationScope scope, string identifier);
}

/// <summary>
/// Result of a task status gate validation.
/// </summary>
/// <param name="IsAllowed">Whether the completion is allowed.</param>
/// <param name="RejectionReason">If not allowed, the reason for rejection.</param>
public sealed record TaskStatusGateResult(bool IsAllowed, string? RejectionReason = null)
{
    /// <summary>Creates a result indicating completion is allowed.</summary>
    public static TaskStatusGateResult Allowed() => new(true);

    /// <summary>Creates a result indicating completion is rejected.</summary>
    public static TaskStatusGateResult Rejected(string reason) => new(false, reason);
}
