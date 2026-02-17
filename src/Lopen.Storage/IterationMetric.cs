namespace Lopen.Storage;

/// <summary>
/// Token usage metrics for a single iteration, stored in metrics.json.
/// </summary>
public sealed record IterationMetric
{
    /// <summary>Input tokens consumed in this iteration.</summary>
    public int InputTokens { get; init; }

    /// <summary>Output tokens produced in this iteration.</summary>
    public int OutputTokens { get; init; }

    /// <summary>Total tokens (input + output) for this iteration.</summary>
    public int TotalTokens { get; init; }

    /// <summary>Context window size at time of this iteration.</summary>
    public int ContextWindowSize { get; init; }

    /// <summary>Whether this iteration used a premium request.</summary>
    public bool IsPremiumRequest { get; init; }
}
