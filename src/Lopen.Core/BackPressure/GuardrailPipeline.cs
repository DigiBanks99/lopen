namespace Lopen.Core.BackPressure;

/// <summary>
/// Pipeline that composes and evaluates all registered guardrails in order.
/// </summary>
internal sealed class GuardrailPipeline : IGuardrailPipeline
{
    private readonly IEnumerable<IGuardrail> _guardrails;

    public GuardrailPipeline(IEnumerable<IGuardrail> guardrails)
    {
        _guardrails = guardrails;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GuardrailResult>> EvaluateAsync(
        GuardrailContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GuardrailResult>();
        var ordered = _guardrails.OrderBy(g => g.Order);

        foreach (var guardrail in ordered)
        {
            var result = await guardrail.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (result is GuardrailResult.Block && guardrail.ShortCircuitOnBlock)
            {
                break;
            }
        }

        return results.AsReadOnly();
    }
}
