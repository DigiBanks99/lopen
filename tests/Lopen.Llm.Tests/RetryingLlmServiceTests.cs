using Lopen.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lopen.Llm.Tests;

public sealed class RetryingLlmServiceTests
{
    private static IOptions<LopenOptions> CreateOptions(Action<ModelOptions>? configure = null)
    {
        var opts = new LopenOptions();
        configure?.Invoke(opts.Models);
        return Options.Create(opts);
    }

    private static IModelSelector CreateSelector(IOptions<LopenOptions> options) =>
        new DefaultModelSelector(options, NullLogger<DefaultModelSelector>.Instance);

    private static RetryingLlmService CreateService(
        ILlmService inner,
        IOptions<LopenOptions>? options = null,
        IModelSelector? selector = null)
    {
        options ??= CreateOptions();
        selector ??= CreateSelector(options);
        return new RetryingLlmService(
            inner, selector, options,
            NullLogger<RetryingLlmService>.Instance);
    }

    // --- Success on first try ---

    [Fact]
    public async Task InvokeAsync_SuccessOnFirstTry_ReturnsResult()
    {
        var inner = new FakeLlmService
        {
            DefaultResult = new LlmInvocationResult("ok",
                new TokenUsage(10, 20, 30, 1000, false), 0, true)
        };

        var sut = CreateService(inner);
        var result = await sut.InvokeAsync("prompt", "claude-opus-4.6", []);

        Assert.Equal("ok", result.Output);
        Assert.Single(inner.Calls);
        Assert.Equal("claude-opus-4.6", inner.Calls[0].model);
    }

    // --- Fallback on model unavailable ---

    [Fact]
    public async Task InvokeAsync_FirstModelUnavailable_FallsBackToNext()
    {
        var inner = new FakeLlmService();
        inner.FailModels.Add("claude-opus-4.6");
        inner.DefaultResult = new LlmInvocationResult("fallback-ok",
            new TokenUsage(5, 10, 15, 500, false), 0, true);

        var options = CreateOptions(m =>
        {
            m.Building = "claude-opus-4.6";
            m.BuildingFallbacks = ["gpt-5"];
        });

        var sut = CreateService(inner, options);
        var result = await sut.InvokeAsync("prompt", "claude-opus-4.6", []);

        Assert.Equal("fallback-ok", result.Output);
        Assert.Equal(2, inner.Calls.Count);
        Assert.Equal("claude-opus-4.6", inner.Calls[0].model);
        Assert.Equal("gpt-5", inner.Calls[1].model);
    }

    // --- All models unavailable throws ---

    [Fact]
    public async Task InvokeAsync_AllModelsUnavailable_Throws()
    {
        var inner = new FakeLlmService();
        inner.FailAllModels = true;

        var options = CreateOptions(m =>
        {
            m.Building = "claude-opus-4.6";
            m.BuildingFallbacks = ["gpt-5"];
            m.GlobalFallback = "claude-sonnet-4";
        });

        var sut = CreateService(inner, options);
        var ex = await Assert.ThrowsAsync<LlmException>(
            () => sut.InvokeAsync("prompt", "claude-opus-4.6", []));

        Assert.True(ex.IsModelUnavailable);
        Assert.Contains("All models unavailable", ex.Message);
        Assert.Contains("claude-opus-4.6", ex.Message);
        Assert.Equal(3, inner.Calls.Count); // opus, gpt-5, sonnet
    }

    // --- Skips duplicate models in chain ---

    [Fact]
    public async Task InvokeAsync_DuplicateInFallbackChain_SkipsDuplicate()
    {
        var inner = new FakeLlmService();
        inner.FailModels.Add("claude-opus-4.6");
        inner.DefaultResult = new LlmInvocationResult("ok",
            new TokenUsage(1, 1, 2, 100, false), 0, true);

        var options = CreateOptions(m =>
        {
            m.Building = "claude-opus-4.6";
            // Fallback is the same as global fallback — should not appear twice
            m.BuildingFallbacks = ["claude-sonnet-4"];
            m.GlobalFallback = "claude-sonnet-4";
        });

        var sut = CreateService(inner, options);
        await sut.InvokeAsync("prompt", "claude-opus-4.6", []);

        Assert.Equal(2, inner.Calls.Count);
        Assert.Equal("claude-opus-4.6", inner.Calls[0].model);
        Assert.Equal("claude-sonnet-4", inner.Calls[1].model);
    }

    // --- Non-model errors propagate immediately ---

    [Fact]
    public async Task InvokeAsync_NonModelError_PropagatesImmediately()
    {
        var inner = new FakeLlmService();
        inner.ThrowNonModelError = true;

        var sut = CreateService(inner);
        var ex = await Assert.ThrowsAsync<LlmException>(
            () => sut.InvokeAsync("prompt", "claude-opus-4.6", []));

        Assert.False(ex.IsModelUnavailable);
        Assert.Single(inner.Calls);
    }

    // --- Cancellation propagates ---

    [Fact]
    public async Task InvokeAsync_Cancellation_PropagatesImmediately()
    {
        var inner = new FakeLlmService();
        inner.ThrowCancellation = true;

        var sut = CreateService(inner);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.InvokeAsync("prompt", "claude-opus-4.6", [], cts.Token));

        Assert.Single(inner.Calls);
    }

    // --- BuildFallbackChain tests ---

    [Fact]
    public void BuildFallbackChain_KnownPhaseModel_ReturnsFullChain()
    {
        var options = CreateOptions(m =>
        {
            m.Research = "gpt-5";
            m.ResearchFallbacks = ["gpt-4.1", "gpt-5-mini"];
            m.GlobalFallback = "claude-sonnet-4";
        });

        var sut = CreateService(new FakeLlmService(), options);
        var chain = sut.BuildFallbackChain("gpt-5");

        Assert.Equal(["gpt-5", "gpt-4.1", "gpt-5-mini", "claude-sonnet-4"], chain);
    }

    [Fact]
    public void BuildFallbackChain_UnknownModel_ReturnsSelfPlusGlobal()
    {
        var options = CreateOptions(m => m.GlobalFallback = "claude-sonnet-4");
        var sut = CreateService(new FakeLlmService(), options);

        var chain = sut.BuildFallbackChain("custom-model");

        Assert.Equal(["custom-model", "claude-sonnet-4"], chain);
    }

    [Fact]
    public void BuildFallbackChain_ModelIsGlobalFallback_ReturnsOnlyOne()
    {
        var options = CreateOptions(m => m.GlobalFallback = "claude-sonnet-4");
        var sut = CreateService(new FakeLlmService(), options);

        var chain = sut.BuildFallbackChain("claude-sonnet-4");

        Assert.Single(chain);
        Assert.Equal("claude-sonnet-4", chain[0]);
    }

    // --- DefaultModelSelector.GetFallbackChain tests ---

    [Fact]
    public void DefaultModelSelector_GetFallbackChain_IncludesConfiguredFallbacks()
    {
        var options = CreateOptions(m =>
        {
            m.Planning = "claude-opus-4.6";
            m.PlanningFallbacks = ["gpt-5", "gpt-4.1"];
            m.GlobalFallback = "claude-sonnet-4";
        });

        var selector = CreateSelector(options);
        var chain = selector.GetFallbackChain(WorkflowPhase.Planning);

        Assert.Equal(["claude-opus-4.6", "gpt-5", "gpt-4.1", "claude-sonnet-4"], chain);
    }

    [Fact]
    public void DefaultModelSelector_GetFallbackChain_EmptyFallbacks_ReturnsPrimaryPlusGlobal()
    {
        var options = CreateOptions(m =>
        {
            m.Building = "gpt-5";
            m.GlobalFallback = "claude-sonnet-4";
        });

        var selector = CreateSelector(options);
        var chain = selector.GetFallbackChain(WorkflowPhase.Building);

        Assert.Equal(["gpt-5", "claude-sonnet-4"], chain);
    }

    [Fact]
    public void DefaultModelSelector_GetFallbackChain_PrimaryIsGlobal_NoDuplicate()
    {
        var options = CreateOptions(m =>
        {
            m.Research = "claude-sonnet-4";
            m.GlobalFallback = "claude-sonnet-4";
        });

        var selector = CreateSelector(options);
        var chain = selector.GetFallbackChain(WorkflowPhase.Research);

        Assert.Single(chain);
        Assert.Equal("claude-sonnet-4", chain[0]);
    }

    // --- LlmException.LooksLikeModelUnavailable tests ---

    [Theory]
    [InlineData("The model 'gpt-9' is unavailable", true)]
    [InlineData("model not found: gpt-9", true)]
    [InlineData("The requested model does not exist", true)]
    [InlineData("model is not available in this region", true)]
    [InlineData("Rate limit exceeded", false)]
    [InlineData("Authentication failed", false)]
    [InlineData("unavailable service", false)] // no "model" keyword
    public void LooksLikeModelUnavailable_DetectsCorrectly(string message, bool expected)
    {
        var ex = new Exception(message);
        Assert.Equal(expected, LlmException.LooksLikeModelUnavailable(ex));
    }

    // --- Fake ILlmService ---

    private sealed class FakeLlmService : ILlmService
    {
        public List<(string prompt, string model, IReadOnlyList<LopenToolDefinition> tools)> Calls { get; } = [];
        public HashSet<string> FailModels { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool FailAllModels { get; set; }
        public bool ThrowNonModelError { get; set; }
        public bool ThrowCancellation { get; set; }

        public LlmInvocationResult? DefaultResult { get; set; }

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt, string model,
            IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((systemPrompt, model, tools));

            if (ThrowCancellation)
                throw new OperationCanceledException();

            if (ThrowNonModelError)
                throw new LlmException("Auth error", model) { IsModelUnavailable = false };

            if (FailAllModels || FailModels.Contains(model))
                throw new LlmException($"SDK invocation failed: The model '{model}' is unavailable", model)
                {
                    IsModelUnavailable = true,
                };

            return Task.FromResult(DefaultResult ?? new LlmInvocationResult("output",
                new TokenUsage(10, 20, 30, 1000, false), 0, true));
        }
    }
}
