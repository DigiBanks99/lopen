namespace Lopen.Core.Workflow;

/// <summary>
/// Severity level for a workflow failure.
/// </summary>
public enum FailureSeverity
{
    /// <summary>Minor warning, informational only.</summary>
    Warning,

    /// <summary>Single task failure — LLM should self-correct inline.</summary>
    TaskFailure,

    /// <summary>Repeated task failures (threshold exceeded) — prompt user for intervention.</summary>
    RepeatedFailure,

    /// <summary>Critical system error — block further progress.</summary>
    Critical
}

/// <summary>
/// The recommended action for a failure.
/// </summary>
public enum FailureAction
{
    /// <summary>Continue with self-correction.</summary>
    SelfCorrect,

    /// <summary>Prompt user for intervention.</summary>
    PromptUser,

    /// <summary>Block further progress.</summary>
    Block
}

/// <summary>
/// Result of failure classification.
/// </summary>
/// <param name="Severity">The severity of the failure.</param>
/// <param name="Action">Recommended action.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="TaskId">The task that failed, if applicable.</param>
/// <param name="ConsecutiveFailures">How many times this task has failed consecutively.</param>
public sealed record FailureClassification(
    FailureSeverity Severity,
    FailureAction Action,
    string Message,
    string? TaskId = null,
    int ConsecutiveFailures = 0);
