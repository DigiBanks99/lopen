namespace Lopen.Core.BackPressure;

/// <summary>
/// A no-op guardrail that always passes. Used as a placeholder when
/// a guardrail's prerequisites are not configured.
/// </summary>
internal sealed class PassThroughGuardrail : IGuardrail
{
    public PassThroughGuardrail(int order = int.MaxValue) => Order = order;
    public int Order { get; }
    public bool ShortCircuitOnBlock => false;
    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<GuardrailResult>(new GuardrailResult.Pass());
}
