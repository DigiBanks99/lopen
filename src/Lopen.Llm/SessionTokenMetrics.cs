namespace Lopen.Llm;

/// <summary>
/// Aggregated token metrics across a session's invocations.
/// </summary>
public sealed record SessionTokenMetrics
{
    /// <summary>Token usage per invocation in order.</summary>
    public IReadOnlyList<TokenUsage> PerIterationTokens { get; init; } = [];

    /// <summary>Total input tokens across all invocations.</summary>
    public int CumulativeInputTokens { get; init; }

    /// <summary>Total output tokens across all invocations.</summary>
    public int CumulativeOutputTokens { get; init; }

    /// <summary>Number of premium API requests consumed.</summary>
    public int PremiumRequestCount { get; init; }
}
