namespace Lopen.Core.BackPressure;

/// <summary>
/// Back-pressure Category 4: Tool Discipline.
/// Detects wasteful tool call patterns and injects corrective instructions.
/// </summary>
internal sealed class ToolDisciplineGuardrail : IGuardrail
{
    private readonly int _toolCallThreshold;

    /// <summary>
    /// Creates a tool discipline guardrail.
    /// </summary>
    /// <param name="toolCallThreshold">Max tool calls per iteration before warning (default: 50).</param>
    public ToolDisciplineGuardrail(int toolCallThreshold = 50)
    {
        if (toolCallThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(toolCallThreshold), "Threshold must be positive.");
        _toolCallThreshold = toolCallThreshold;
    }

    public int Order => 400;
    public bool ShortCircuitOnBlock => false;

    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default)
    {
        GuardrailResult result;

        if (context.ToolCallCount > _toolCallThreshold * 2)
        {
            result = new GuardrailResult.Warn(
                $"Excessive tool calls ({context.ToolCallCount}) in this iteration. " +
                "Consider a more focused approach — read files once, make targeted changes, " +
                "and verify. Avoid reading the same file repeatedly.");
        }
        else if (context.ToolCallCount > _toolCallThreshold)
        {
            result = new GuardrailResult.Warn(
                $"High tool call count ({context.ToolCallCount}/{_toolCallThreshold}). " +
                "Ensure each tool call serves a purpose.");
        }
        else
        {
            result = new GuardrailResult.Pass();
        }

        return Task.FromResult(result);
    }
}
