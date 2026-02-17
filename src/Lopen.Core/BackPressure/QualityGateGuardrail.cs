namespace Lopen.Core.BackPressure;

/// <summary>
/// Back-pressure Category 3: Quality Gates.
/// Enforces acceptance criteria verification at module and component completion boundaries.
/// </summary>
internal sealed class QualityGateGuardrail : IGuardrail
{
    private readonly Func<GuardrailContext, bool> _isCompletionBoundary;
    private readonly Func<GuardrailContext, bool> _hasPassingVerification;

    /// <summary>
    /// Creates a quality gate guardrail.
    /// </summary>
    /// <param name="isCompletionBoundary">Returns true if the context represents a completion boundary.</param>
    /// <param name="hasPassingVerification">Returns true if verification has passed for this scope.</param>
    public QualityGateGuardrail(
        Func<GuardrailContext, bool> isCompletionBoundary,
        Func<GuardrailContext, bool> hasPassingVerification)
    {
        _isCompletionBoundary = isCompletionBoundary ?? throw new ArgumentNullException(nameof(isCompletionBoundary));
        _hasPassingVerification = hasPassingVerification ?? throw new ArgumentNullException(nameof(hasPassingVerification));
    }

    public int Order => 300;
    public bool ShortCircuitOnBlock => true;

    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default)
    {
        GuardrailResult result;

        if (!_isCompletionBoundary(context))
        {
            result = new GuardrailResult.Pass();
        }
        else if (_hasPassingVerification(context))
        {
            result = new GuardrailResult.Pass();
        }
        else
        {
            result = new GuardrailResult.Block(
                $"Quality gate: completion of '{context.TaskName ?? context.ModuleName}' requires " +
                "passing oracle verification. Run verify_task_completion or verify_component_completion first.");
        }

        return Task.FromResult(result);
    }
}
