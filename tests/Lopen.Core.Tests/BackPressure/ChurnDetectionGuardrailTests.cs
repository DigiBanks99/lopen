using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class ChurnDetectionGuardrailTests
{
    [Fact]
    public async Task EvaluateAsync_BelowThreshold_ReturnsPass()
    {
        var guardrail = new ChurnDetectionGuardrail(failureThreshold: 3);
        var context = new GuardrailContext("mod", "task-1", IterationCount: 1, ToolCallCount: 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_OneBeforeThreshold_ReturnsWarn()
    {
        var guardrail = new ChurnDetectionGuardrail(failureThreshold: 3);
        var context = new GuardrailContext("mod", "task-1", IterationCount: 2, ToolCallCount: 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Warn>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AtThreshold_ReturnsBlock()
    {
        var guardrail = new ChurnDetectionGuardrail(failureThreshold: 3);
        var context = new GuardrailContext("mod", "task-1", IterationCount: 3, ToolCallCount: 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Block>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AboveThreshold_ReturnsBlock()
    {
        var guardrail = new ChurnDetectionGuardrail(failureThreshold: 3);
        var context = new GuardrailContext("mod", "task-1", IterationCount: 5, ToolCallCount: 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Block>(result);
    }

    [Fact]
    public async Task EvaluateAsync_BlockMessage_ContainsTaskName()
    {
        var guardrail = new ChurnDetectionGuardrail(failureThreshold: 3);
        var context = new GuardrailContext("mod", "my-task", IterationCount: 3, ToolCallCount: 5);

        var result = await guardrail.EvaluateAsync(context);

        var block = Assert.IsType<GuardrailResult.Block>(result);
        Assert.Contains("my-task", block.Message);
    }

    [Fact]
    public void Order_Returns200()
    {
        var guardrail = new ChurnDetectionGuardrail();

        Assert.Equal(200, guardrail.Order);
    }

    [Fact]
    public void ShortCircuitOnBlock_ReturnsFalse()
    {
        var guardrail = new ChurnDetectionGuardrail();

        Assert.False(guardrail.ShortCircuitOnBlock);
    }

    [Fact]
    public void Constructor_ZeroThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ChurnDetectionGuardrail(failureThreshold: 0));
    }

    [Fact]
    public void ImplementsIGuardrail()
    {
        Assert.IsAssignableFrom<IGuardrail>(new ChurnDetectionGuardrail());
    }
}
