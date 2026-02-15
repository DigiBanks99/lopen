using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class ToolDisciplineGuardrailTests
{
    [Fact]
    public async Task EvaluateAsync_BelowThreshold_ReturnsPass()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 30);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AboveThreshold_ReturnsWarn()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 60);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Warn>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AboveDoubleThreshold_ReturnsStrongerWarn()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 110);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("Excessive", warn.Message);
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyAtThreshold_ReturnsPass()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 50);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_WarnMessage_ContainsCount()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 50);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 75);

        var result = await guardrail.EvaluateAsync(context);

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("75", warn.Message);
    }

    [Fact]
    public void Order_Returns400()
    {
        var guardrail = new ToolDisciplineGuardrail();

        Assert.Equal(400, guardrail.Order);
    }

    [Fact]
    public void ShortCircuitOnBlock_ReturnsFalse()
    {
        var guardrail = new ToolDisciplineGuardrail();

        Assert.False(guardrail.ShortCircuitOnBlock);
    }

    [Fact]
    public void Constructor_ZeroThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolDisciplineGuardrail(toolCallThreshold: 0));
    }

    [Fact]
    public void ImplementsIGuardrail()
    {
        Assert.IsAssignableFrom<IGuardrail>(new ToolDisciplineGuardrail());
    }
}
