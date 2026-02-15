namespace Lopen.Core.BackPressure;

/// <summary>
/// Back-pressure Category 2: Progress Integrity — Churn Detection.
/// Detects when the same task fails repeatedly and escalates.
/// </summary>
internal sealed class ChurnDetectionGuardrail : IGuardrail
{
    private readonly int _failureThreshold;

    /// <summary>
    /// Creates a churn detection guardrail.
    /// </summary>
    /// <param name="failureThreshold">Number of task failures before escalation (default: 3).</param>
    public ChurnDetectionGuardrail(int failureThreshold = 3)
    {
        if (failureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Threshold must be positive.");
        _failureThreshold = failureThreshold;
    }

    public int Order => 200;
    public bool ShortCircuitOnBlock => false;

    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default)
    {
        GuardrailResult result;

        if (context.IterationCount >= _failureThreshold)
        {
            result = new GuardrailResult.Block(
                $"Task '{context.TaskName ?? "unknown"}' has been attempted {context.IterationCount} times " +
                $"(threshold: {_failureThreshold}). User intervention recommended.");
        }
        else if (context.IterationCount >= _failureThreshold - 1)
        {
            result = new GuardrailResult.Warn(
                $"Task '{context.TaskName ?? "unknown"}' approaching failure threshold " +
                $"({context.IterationCount}/{_failureThreshold}).");
        }
        else
        {
            result = new GuardrailResult.Pass();
        }

        return Task.FromResult(result);
    }
}
