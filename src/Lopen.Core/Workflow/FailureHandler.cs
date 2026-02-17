using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Classifies failures based on pattern detection:
/// - Single failure: inline self-correction
/// - Repeated failure (≥ threshold): prompt user for intervention
/// - Critical system error: block further progress
/// - Warning: informational only
/// </summary>
internal sealed class FailureHandler : IFailureHandler
{
    private readonly int _failureThreshold;
    private readonly ILogger<FailureHandler> _logger;
    private readonly Dictionary<string, int> _failureCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new failure handler.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="failureThreshold">Number of consecutive failures before escalating to user intervention. Default: 3.</param>
    public FailureHandler(ILogger<FailureHandler> logger, int failureThreshold = 3)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _failureThreshold = failureThreshold > 0
            ? failureThreshold
            : throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Must be positive");
    }

    public FailureClassification RecordFailure(string taskId, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        _failureCounts.TryGetValue(taskId, out var count);
        count++;
        _failureCounts[taskId] = count;

        if (count >= _failureThreshold)
        {
            _logger.LogWarning(
                "Task {TaskId} has failed {Count} times (threshold: {Threshold}) — user intervention needed",
                taskId, count, _failureThreshold);

            return new FailureClassification(
                FailureSeverity.RepeatedFailure,
                FailureAction.PromptUser,
                $"Task '{taskId}' has failed {count} consecutive times. User intervention recommended.",
                taskId,
                count);
        }

        _logger.LogInformation(
            "Task {TaskId} failed ({Count}/{Threshold}) — self-correcting inline",
            taskId, count, _failureThreshold);

        return new FailureClassification(
            FailureSeverity.TaskFailure,
            FailureAction.SelfCorrect,
            $"Task '{taskId}' failed (attempt {count}/{_failureThreshold}). Self-correcting.",
            taskId,
            count);
    }

    public FailureClassification RecordCriticalError(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        _logger.LogError("Critical error — blocking: {Message}", errorMessage);

        return new FailureClassification(
            FailureSeverity.Critical,
            FailureAction.Block,
            errorMessage);
    }

    public FailureClassification RecordWarning(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        _logger.LogWarning("Warning: {Message}", message);

        return new FailureClassification(
            FailureSeverity.Warning,
            FailureAction.SelfCorrect,
            message);
    }

    public void ResetFailureCount(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        _failureCounts.Remove(taskId);
    }

    public int GetFailureCount(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _failureCounts.TryGetValue(taskId, out var count) ? count : 0;
    }
}
