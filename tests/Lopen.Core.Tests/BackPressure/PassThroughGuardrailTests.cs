using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class PassThroughGuardrailTests
{
    private static GuardrailContext CreateContext() =>
        new("test-module", "test-task", IterationCount: 1, ToolCallCount: 5);

    [Fact]
    public async Task EvaluateAsync_AlwaysReturnsPass()
    {
        var guardrail = new PassThroughGuardrail();

        var result = await guardrail.EvaluateAsync(CreateContext());

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public void Constructor_DefaultOrder_IsMaxValue()
    {
        var guardrail = new PassThroughGuardrail();

        Assert.Equal(int.MaxValue, guardrail.Order);
    }

    [Fact]
    public void Constructor_CustomOrder_IsUsed()
    {
        var guardrail = new PassThroughGuardrail(order: 42);

        Assert.Equal(42, guardrail.Order);
    }
}
