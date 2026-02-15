namespace Lopen.Core.BackPressure;

/// <summary>
/// A single guardrail that evaluates a condition and returns a result.
/// Guardrails are composed into a pipeline and evaluated in order.
/// </summary>
public interface IGuardrail
{
    /// <summary>
    /// Evaluation order. Lower values are evaluated first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Whether to stop evaluating subsequent guardrails when this one blocks.
    /// </summary>
    bool ShortCircuitOnBlock { get; }

    /// <summary>
    /// Evaluates this guardrail against the current context.
    /// </summary>
    /// <param name="context">Contextual information for evaluation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the guardrail evaluation.</returns>
    Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default);
}
