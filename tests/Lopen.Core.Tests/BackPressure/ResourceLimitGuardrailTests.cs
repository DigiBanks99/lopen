using Lopen.Core.BackPressure;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.BackPressure;

public class ResourceLimitGuardrailTests
{
    private readonly FakeTokenTracker _tracker = new();

    private ResourceLimitGuardrail CreateGuardrail(int budget = 100, double warn = 0.80, double block = 0.90) =>
        new(_tracker, NullLogger<ResourceLimitGuardrail>.Instance, budget, warn, block);

    private static GuardrailContext CreateContext() =>
        new("test-module", "test-task", IterationCount: 1, ToolCallCount: 5);

    [Fact]
    public async Task EvaluateAsync_BelowWarn_ReturnsPass()
    {
        _tracker.PremiumRequests = 50;
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AtWarnThreshold_ReturnsWarn()
    {
        _tracker.PremiumRequests = 80;
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        Assert.IsType<GuardrailResult.Warn>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AtBlockThreshold_ReturnsBlock()
    {
        _tracker.PremiumRequests = 90;
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        Assert.IsType<GuardrailResult.Block>(result);
    }

    [Fact]
    public async Task EvaluateAsync_AboveBlockThreshold_ReturnsBlock()
    {
        _tracker.PremiumRequests = 95;
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        Assert.IsType<GuardrailResult.Block>(result);
    }

    [Fact]
    public async Task EvaluateAsync_ZeroUsage_ReturnsPass()
    {
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_WarnMessage_ContainsBudgetInfo()
    {
        _tracker.PremiumRequests = 85;
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        var warn = Assert.IsType<GuardrailResult.Warn>(result);
        Assert.Contains("85", warn.Message);
        Assert.Contains("100", warn.Message);
    }

    [Fact]
    public async Task EvaluateAsync_BlockMessage_ContainsBudgetInfo()
    {
        _tracker.PremiumRequests = 92;
        var guardrail = CreateGuardrail(budget: 100);

        var result = await guardrail.EvaluateAsync(CreateContext());

        var block = Assert.IsType<GuardrailResult.Block>(result);
        Assert.Contains("92", block.Message);
        Assert.Contains("100", block.Message);
    }

    [Fact]
    public void Order_Returns100()
    {
        var guardrail = CreateGuardrail();

        Assert.Equal(100, guardrail.Order);
    }

    [Fact]
    public void ShortCircuitOnBlock_ReturnsTrue()
    {
        var guardrail = CreateGuardrail();

        Assert.True(guardrail.ShortCircuitOnBlock);
    }

    [Fact]
    public void Constructor_ZeroBudget_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateGuardrail(budget: 0));
    }

    [Fact]
    public void Constructor_NegativeBudget_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateGuardrail(budget: -1));
    }

    [Fact]
    public void Constructor_BlockLessThanWarn_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CreateGuardrail(budget: 100, warn: 0.90, block: 0.80));
    }

    [Fact]
    public void Constructor_NullTracker_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ResourceLimitGuardrail(null!, NullLogger<ResourceLimitGuardrail>.Instance, 100));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ResourceLimitGuardrail(_tracker, null!, 100));
    }

    [Fact]
    public void ImplementsIGuardrail()
    {
        var guardrail = CreateGuardrail();

        Assert.IsAssignableFrom<IGuardrail>(guardrail);
    }

    private sealed class FakeTokenTracker : ITokenTracker
    {
        public int PremiumRequests { get; set; }

        public void RecordUsage(TokenUsage usage) { }

        public SessionTokenMetrics GetSessionMetrics() =>
            new() { PremiumRequestCount = PremiumRequests };

        public void ResetSession() => PremiumRequests = 0;
    }
}
