using Lopen.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lopen.Llm;

/// <summary>
/// Selects models per workflow phase from configuration, with fallback support.
/// </summary>
internal sealed class DefaultModelSelector : IModelSelector
{
    internal const string FallbackModel = "claude-sonnet-4";

    private readonly ModelOptions _modelOptions;
    private readonly ILogger<DefaultModelSelector> _logger;

    public DefaultModelSelector(IOptions<LopenOptions> options, ILogger<DefaultModelSelector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _modelOptions = options.Value.Models;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ModelFallbackResult SelectModel(WorkflowPhase phase)
    {
        var configured = phase switch
        {
            WorkflowPhase.RequirementGathering => _modelOptions.RequirementGathering,
            WorkflowPhase.Planning => _modelOptions.Planning,
            WorkflowPhase.Building => _modelOptions.Building,
            WorkflowPhase.Research => _modelOptions.Research,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown workflow phase"),
        };

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new ModelFallbackResult(configured, WasFallback: false);
        }

        _logger.LogWarning(
            "No model configured for phase {Phase}, falling back to {FallbackModel}",
            phase, FallbackModel);

        return new ModelFallbackResult(FallbackModel, WasFallback: true, OriginalModel: configured);
    }
}
