namespace Lopen.Core.BackPressure;

/// <summary>
/// Composes multiple guardrails into an ordered evaluation pipeline.
/// </summary>
public interface IGuardrailPipeline
{
    /// <summary>
    /// Evaluates all registered guardrails in order.
    /// </summary>
    /// <param name="context">Contextual information for evaluation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated results from all evaluated guardrails.</returns>
    Task<IReadOnlyList<GuardrailResult>> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default);
}
