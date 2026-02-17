using Lopen.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lopen.Llm;

/// <summary>
/// Decorator that retries LLM invocations with fallback models when the
/// requested model is unavailable at runtime (LLM-11).
/// </summary>
internal sealed class RetryingLlmService : ILlmService
{
    private readonly ILlmService _inner;
    private readonly IModelSelector _modelSelector;
    private readonly ModelOptions _modelOptions;
    private readonly ILogger<RetryingLlmService> _logger;

    public RetryingLlmService(
        ILlmService inner,
        IModelSelector modelSelector,
        IOptions<LopenOptions> options,
        ILogger<RetryingLlmService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
        ArgumentNullException.ThrowIfNull(options);
        _modelOptions = options.Value.Models;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LlmInvocationResult> InvokeAsync(
        string systemPrompt,
        string model,
        IReadOnlyList<LopenToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        var chain = BuildFallbackChain(model);

        LlmException? lastException = null;

        foreach (var candidate in chain)
        {
            try
            {
                if (candidate != model)
                {
                    _logger.LogWarning(
                        "Model {OriginalModel} unavailable, trying fallback {FallbackModel}",
                        model, candidate);
                }

                return await _inner.InvokeAsync(systemPrompt, candidate, tools, cancellationToken);
            }
            catch (LlmException ex) when (ex.IsModelUnavailable)
            {
                _logger.LogWarning(
                    "Model {Model} unavailable: {Message}",
                    candidate, ex.Message);
                lastException = ex;
            }
        }

        throw new LlmException(
            $"All models unavailable. Tried: {string.Join(", ", chain)}",
            model,
            lastException!)
        {
            IsModelUnavailable = true,
        };
    }

    /// <summary>
    /// Builds the ordered fallback chain for a model.
    /// Matches the model to a workflow phase for per-phase fallbacks,
    /// preferring the phase with the longest configured chain.
    /// </summary>
    internal IReadOnlyList<string> BuildFallbackChain(string model)
    {
        IReadOnlyList<string>? bestChain = null;

        foreach (var phase in Enum.GetValues<WorkflowPhase>())
        {
            var primary = _modelSelector.SelectModel(phase).SelectedModel;
            if (string.Equals(primary, model, StringComparison.OrdinalIgnoreCase))
            {
                var chain = _modelSelector.GetFallbackChain(phase);
                if (bestChain is null || chain.Count > bestChain.Count)
                    bestChain = chain;
            }
        }

        if (bestChain is not null)
            return bestChain;

        // Model doesn't match any phase primary — return [model, globalFallback]
        var fallback = new List<string> { model };
        if (!string.Equals(model, _modelOptions.GlobalFallback, StringComparison.OrdinalIgnoreCase))
            fallback.Add(_modelOptions.GlobalFallback);

        return fallback;
    }
}
