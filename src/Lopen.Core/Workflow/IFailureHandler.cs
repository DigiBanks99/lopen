namespace Lopen.Core.Workflow;

/// <summary>
/// Classifies failures and determines appropriate response actions.
/// </summary>
public interface IFailureHandler
{
    /// <summary>
    /// Records a task failure and classifies it based on pattern detection.
    /// </summary>
    /// <param name="taskId">The task that failed.</param>
    /// <param name="errorMessage">Description of the failure.</param>
    /// <returns>Classification with recommended action.</returns>
    FailureClassification RecordFailure(string taskId, string errorMessage);

    /// <summary>
    /// Records a critical system error.
    /// </summary>
    /// <param name="errorMessage">Description of the error.</param>
    /// <returns>Classification with Block action.</returns>
    FailureClassification RecordCriticalError(string errorMessage);

    /// <summary>
    /// Records a warning (informational, no action needed).
    /// </summary>
    /// <param name="message">Warning message.</param>
    /// <returns>Classification with SelfCorrect action.</returns>
    FailureClassification RecordWarning(string message);

    /// <summary>
    /// Resets the failure count for a task (e.g., after successful completion).
    /// </summary>
    /// <param name="taskId">The task to reset.</param>
    void ResetFailureCount(string taskId);

    /// <summary>
    /// Gets the consecutive failure count for a task.
    /// </summary>
    /// <param name="taskId">The task to check.</param>
    /// <returns>Number of consecutive failures.</returns>
    int GetFailureCount(string taskId);
}
