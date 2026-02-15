using Lopen.Core.BackPressure;

namespace Lopen.Core.Tests.BackPressure;

public class GuardrailPipelineTests
{
    [Fact]
    public async Task EvaluateAsync_NoGuardrails_ReturnsEmpty()
    {
        var pipeline = new GuardrailPipeline([]);
        var context = new GuardrailContext("auth", null, 1, 0);

        var results = await pipeline.EvaluateAsync(context);

        Assert.Empty(results);
    }

    [Fact]
    public async Task EvaluateAsync_SinglePassGuardrail_ReturnsSingleResult()
    {
        var guardrail = new TestGuardrail(1, false, new GuardrailResult.Pass());
        var pipeline = new GuardrailPipeline([guardrail]);
        var context = new GuardrailContext("auth", null, 1, 0);

        var results = await pipeline.EvaluateAsync(context);

        Assert.Single(results);
        Assert.IsType<GuardrailResult.Pass>(results[0]);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleGuardrails_EvaluatesInOrder()
    {
        var g1 = new TestGuardrail(1, false, new GuardrailResult.Pass());
        var g2 = new TestGuardrail(2, false, new GuardrailResult.Warn("warning"));
        var g3 = new TestGuardrail(3, false, new GuardrailResult.Pass());
        var pipeline = new GuardrailPipeline([g3, g1, g2]); // out of order
        var context = new GuardrailContext("auth", null, 1, 0);

        var results = await pipeline.EvaluateAsync(context);

        Assert.Equal(3, results.Count);
        Assert.IsType<GuardrailResult.Pass>(results[0]);
        Assert.IsType<GuardrailResult.Warn>(results[1]);
        Assert.IsType<GuardrailResult.Pass>(results[2]);
    }

    [Fact]
    public async Task EvaluateAsync_BlockWithShortCircuit_StopsEvaluation()
    {
        var g1 = new TestGuardrail(1, false, new GuardrailResult.Pass());
        var g2 = new TestGuardrail(2, true, new GuardrailResult.Block("blocked"));
        var g3 = new TestGuardrail(3, false, new GuardrailResult.Pass());
        var pipeline = new GuardrailPipeline([g1, g2, g3]);
        var context = new GuardrailContext("auth", null, 1, 0);

        var results = await pipeline.EvaluateAsync(context);

        Assert.Equal(2, results.Count);
        Assert.IsType<GuardrailResult.Pass>(results[0]);
        Assert.IsType<GuardrailResult.Block>(results[1]);
    }

    [Fact]
    public async Task EvaluateAsync_BlockWithoutShortCircuit_ContinuesEvaluation()
    {
        var g1 = new TestGuardrail(1, false, new GuardrailResult.Block("blocked"));
        var g2 = new TestGuardrail(2, false, new GuardrailResult.Pass());
        var pipeline = new GuardrailPipeline([g1, g2]);
        var context = new GuardrailContext("auth", null, 1, 0);

        var results = await pipeline.EvaluateAsync(context);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void ImplementsInterface()
    {
        var pipeline = new GuardrailPipeline([]);
        Assert.IsAssignableFrom<IGuardrailPipeline>(pipeline);
    }

    private sealed class TestGuardrail(int order, bool shortCircuit, GuardrailResult result)
        : IGuardrail
    {
        public int Order => order;
        public bool ShortCircuitOnBlock => shortCircuit;

        public Task<GuardrailResult> EvaluateAsync(
            GuardrailContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}
