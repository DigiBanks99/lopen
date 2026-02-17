using Lopen.Core.BackPressure;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Core.Tests.BackPressure;

public class GuardrailIntegrationTests
{
    [Fact]
    public async Task ChurnEscalation_PassWarnBlock_ThroughPipeline()
    {
        // Default threshold = 3: Pass at <2, Warn at 2, Block at >=3
        var churn = new ChurnDetectionGuardrail(failureThreshold: 3);
        var pipeline = new GuardrailPipeline([churn]);

        // Iteration 1 — Pass
        var results1 = await pipeline.EvaluateAsync(new GuardrailContext("mod", "task-1", IterationCount: 1, ToolCallCount: 5));
        Assert.Single(results1);
        Assert.IsType<GuardrailResult.Pass>(results1[0]);

        // Iteration 2 — Warn (threshold - 1)
        var results2 = await pipeline.EvaluateAsync(new GuardrailContext("mod", "task-1", IterationCount: 2, ToolCallCount: 5));
        Assert.Single(results2);
        Assert.IsType<GuardrailResult.Warn>(results2[0]);

        // Iteration 3 — Block (at threshold)
        var results3 = await pipeline.EvaluateAsync(new GuardrailContext("mod", "task-1", IterationCount: 3, ToolCallCount: 5));
        Assert.Single(results3);
        Assert.IsType<GuardrailResult.Block>(results3[0]);

        // Iteration 5 — Block (above threshold)
        var results5 = await pipeline.EvaluateAsync(new GuardrailContext("mod", "task-1", IterationCount: 5, ToolCallCount: 5));
        Assert.Single(results5);
        Assert.IsType<GuardrailResult.Block>(results5[0]);
    }

    [Fact]
    public async Task QualityGate_BlocksFalseCompletion_ThroughPipeline()
    {
        var churn = new ChurnDetectionGuardrail(failureThreshold: 10);
        var qualityGate = new QualityGateGuardrail(
            isCompletionBoundary: ctx => ctx.TaskName is not null,
            hasPassingVerification: _ => false);
        var pipeline = new GuardrailPipeline([churn, qualityGate]);
        var context = new GuardrailContext("mod", "unverified-task", IterationCount: 1, ToolCallCount: 2);

        var results = await pipeline.EvaluateAsync(context);

        // Churn passes (iteration 1, well below threshold 10), then QualityGate blocks
        Assert.Equal(2, results.Count);
        Assert.IsType<GuardrailResult.Pass>(results[0]);
        var block = Assert.IsType<GuardrailResult.Block>(results[1]);
        Assert.Contains("unverified-task", block.Message);
    }

    [Fact]
    public async Task ShortCircuit_QualityGateBlocks_StopsDownstreamGuardrails()
    {
        // QualityGateGuardrail has ShortCircuitOnBlock = true (Order 300)
        var qualityGate = new QualityGateGuardrail(
            isCompletionBoundary: _ => true,
            hasPassingVerification: _ => false);
        var counter = new CountingGuardrail(order: 400);
        var pipeline = new GuardrailPipeline([qualityGate, counter]);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 0);

        var results = await pipeline.EvaluateAsync(context);

        // QualityGate blocks with short-circuit — downstream guardrail never called
        Assert.Single(results);
        Assert.IsType<GuardrailResult.Block>(results[0]);
        Assert.Equal(0, counter.CallCount);
    }

    [Fact]
    public async Task ChurnBlock_DoesNotShortCircuit_DownstreamStillRuns()
    {
        // ChurnDetectionGuardrail has ShortCircuitOnBlock = false (Order 200)
        var churn = new ChurnDetectionGuardrail(failureThreshold: 1);
        var counter = new CountingGuardrail(order: 500);
        var pipeline = new GuardrailPipeline([churn, counter]);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 0);

        var results = await pipeline.EvaluateAsync(context);

        // Churn blocks but does NOT short-circuit — downstream guardrail runs
        Assert.Equal(2, results.Count);
        Assert.IsType<GuardrailResult.Block>(results[0]);
        Assert.IsType<GuardrailResult.Pass>(results[1]);
        Assert.Equal(1, counter.CallCount);
    }

    [Fact]
    public void Pipeline_AllGuardrails_ViaRealDI()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<IGuardrailPipeline>();

        Assert.NotNull(pipeline);
        // Verify multiple guardrails are registered
        var guardrails = provider.GetServices<IGuardrail>().ToList();
        Assert.True(guardrails.Count >= 2, $"Expected at least 2 guardrails, got {guardrails.Count}");
    }

    [Fact]
    public async Task CancellationToken_PropagatesThroughPipeline()
    {
        var cts = new CancellationTokenSource();
        var cancellingGuardrail = new CancellingGuardrail(order: 100, cts);
        var counter = new CountingGuardrail(order: 200);
        var pipeline = new GuardrailPipeline([cancellingGuardrail, counter]);
        var context = new GuardrailContext("mod", "task", IterationCount: 1, ToolCallCount: 0);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => pipeline.EvaluateAsync(context, cts.Token));

        Assert.Equal(0, counter.CallCount);
    }

    /// <summary>Guardrail that counts how many times it was called.</summary>
    private sealed class CountingGuardrail(int order) : IGuardrail
    {
        public int Order => order;
        public bool ShortCircuitOnBlock => false;
        public int CallCount { get; private set; }

        public Task<GuardrailResult> EvaluateAsync(
            GuardrailContext context, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<GuardrailResult>(new GuardrailResult.Pass());
        }
    }

    /// <summary>Guardrail that cancels the token before throwing.</summary>
    private sealed class CancellingGuardrail(int order, CancellationTokenSource cts) : IGuardrail
    {
        public int Order => order;
        public bool ShortCircuitOnBlock => false;

        public Task<GuardrailResult> EvaluateAsync(
            GuardrailContext context, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<GuardrailResult>(new GuardrailResult.Pass());
        }
    }
}
