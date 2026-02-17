using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class QualityGateGuardrailTests
{
    [Fact]
    public async Task EvaluateAsync_NotCompletionBoundary_ReturnsPass()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: _ => false,
            hasPassingVerification: _ => false);
        var context = new GuardrailContext("mod", "task", 1, 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_CompletionBoundaryWithVerification_ReturnsPass()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: _ => true,
            hasPassingVerification: _ => true);
        var context = new GuardrailContext("mod", "task", 1, 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Pass>(result);
    }

    [Fact]
    public async Task EvaluateAsync_CompletionBoundaryWithoutVerification_ReturnsBlock()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: _ => true,
            hasPassingVerification: _ => false);
        var context = new GuardrailContext("mod", "task", 1, 5);

        var result = await guardrail.EvaluateAsync(context);

        Assert.IsType<GuardrailResult.Block>(result);
    }

    [Fact]
    public async Task EvaluateAsync_BlockMessage_ContainsTaskName()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: _ => true,
            hasPassingVerification: _ => false);
        var context = new GuardrailContext("mod", "my-task", 1, 5);

        var result = await guardrail.EvaluateAsync(context);

        var block = Assert.IsType<GuardrailResult.Block>(result);
        Assert.Contains("my-task", block.Message);
    }

    [Fact]
    public async Task EvaluateAsync_NoTaskName_UsesModuleName()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: _ => true,
            hasPassingVerification: _ => false);
        var context = new GuardrailContext("auth-module", null, 1, 5);

        var result = await guardrail.EvaluateAsync(context);

        var block = Assert.IsType<GuardrailResult.Block>(result);
        Assert.Contains("auth-module", block.Message);
    }

    [Fact]
    public void Order_Returns300()
    {
        var guardrail = new QualityGateGuardrail(_ => false, _ => false);

        Assert.Equal(300, guardrail.Order);
    }

    [Fact]
    public void ShortCircuitOnBlock_ReturnsTrue()
    {
        var guardrail = new QualityGateGuardrail(_ => false, _ => false);

        Assert.True(guardrail.ShortCircuitOnBlock);
    }

    [Fact]
    public void Constructor_NullIsCompletionBoundary_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new QualityGateGuardrail(null!, _ => false));
    }

    [Fact]
    public void Constructor_NullHasPassingVerification_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new QualityGateGuardrail(_ => false, null!));
    }

    [Fact]
    public void ImplementsIGuardrail()
    {
        Assert.IsAssignableFrom<IGuardrail>(new QualityGateGuardrail(_ => false, _ => false));
    }
}
