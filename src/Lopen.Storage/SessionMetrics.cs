namespace Lopen.Storage;

/// <summary>
/// Represents the persisted token usage and request metrics for a session.
/// </summary>
public sealed record SessionMetrics
{
    /// <summary>The unique session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Total input tokens consumed across all iterations.</summary>
    public long CumulativeInputTokens { get; init; }

    /// <summary>Total output tokens consumed across all iterations.</summary>
    public long CumulativeOutputTokens { get; init; }

    /// <summary>Total premium requests used.</summary>
    public int PremiumRequestCount { get; init; }

    /// <summary>Number of completed iterations.</summary>
    public int IterationCount { get; init; }

    /// <summary>The timestamp of the last metrics update.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
