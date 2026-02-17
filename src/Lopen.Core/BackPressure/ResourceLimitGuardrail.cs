using Lopen.Llm;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.BackPressure;

/// <summary>
/// Back-pressure Category 1: Resource Limits.
/// Warns at the warning threshold and blocks at the block threshold based on premium request usage.
/// </summary>
internal sealed class ResourceLimitGuardrail : IGuardrail
{
    internal const double DefaultWarnThreshold = 0.80;
    internal const double DefaultBlockThreshold = 0.90;

    private readonly ITokenTracker _tokenTracker;
    private readonly ILogger<ResourceLimitGuardrail> _logger;
    private readonly int _premiumRequestBudget;
    private readonly double _warnThreshold;
    private readonly double _blockThreshold;

    public ResourceLimitGuardrail(
        ITokenTracker tokenTracker,
        ILogger<ResourceLimitGuardrail> logger,
        int premiumRequestBudget,
        double warnThreshold = DefaultWarnThreshold,
        double blockThreshold = DefaultBlockThreshold)
    {
        _tokenTracker = tokenTracker ?? throw new ArgumentNullException(nameof(tokenTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (premiumRequestBudget <= 0)
            throw new ArgumentOutOfRangeException(nameof(premiumRequestBudget), "Budget must be positive.");
        if (warnThreshold is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(warnThreshold), "Must be between 0 and 1.");
        if (blockThreshold is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(blockThreshold), "Must be between 0 and 1.");
        if (blockThreshold <= warnThreshold)
            throw new ArgumentException("Block threshold must be greater than warn threshold.");

        _premiumRequestBudget = premiumRequestBudget;
        _warnThreshold = warnThreshold;
        _blockThreshold = blockThreshold;
    }

    public int Order => 100;

    public bool ShortCircuitOnBlock => true;

    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default)
    {
        var metrics = _tokenTracker.GetSessionMetrics();
        var used = metrics.PremiumRequestCount;
        var ratio = (double)used / _premiumRequestBudget;

        GuardrailResult result;

        if (ratio >= _blockThreshold)
        {
            _logger.LogWarning(
                "Premium request budget at {Percent:P0} ({Used}/{Budget}) — blocking",
                ratio, used, _premiumRequestBudget);
            result = new GuardrailResult.Block(
                $"Premium request budget exceeded ({used}/{_premiumRequestBudget}). " +
                "User confirmation required to continue.");
        }
        else if (ratio >= _warnThreshold)
        {
            _logger.LogWarning(
                "Premium request budget at {Percent:P0} ({Used}/{Budget}) — warning",
                ratio, used, _premiumRequestBudget);
            result = new GuardrailResult.Warn(
                $"Approaching premium request budget ({used}/{_premiumRequestBudget}).");
        }
        else
        {
            result = new GuardrailResult.Pass();
        }

        return Task.FromResult(result);
    }
}
