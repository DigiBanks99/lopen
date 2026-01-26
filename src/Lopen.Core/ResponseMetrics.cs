namespace Lopen.Core;

/// <summary>
/// Metrics for a single Copilot response.
/// </summary>
public sealed record ResponseMetrics
{
    /// <summary>When the request was sent.</summary>
    public DateTimeOffset RequestTime { get; init; }

    /// <summary>When the first token was received. Null if no tokens received.</summary>
    public DateTimeOffset? FirstTokenTime { get; init; }

    /// <summary>When the response completed.</summary>
    public DateTimeOffset? CompletionTime { get; init; }

    /// <summary>Time from request to first token.</summary>
    public TimeSpan? TimeToFirstToken => FirstTokenTime.HasValue
        ? FirstTokenTime.Value - RequestTime
        : null;

    /// <summary>Total response time from request to completion.</summary>
    public TimeSpan? TotalTime => CompletionTime.HasValue
        ? CompletionTime.Value - RequestTime
        : null;

    /// <summary>Number of tokens received.</summary>
    public int TokenCount { get; init; }

    /// <summary>Total bytes received.</summary>
    public long BytesReceived { get; init; }

    /// <summary>Tokens per second (streaming throughput).</summary>
    public double? TokensPerSecond
    {
        get
        {
            if (!FirstTokenTime.HasValue || !CompletionTime.HasValue || TokenCount <= 1)
                return null;

            var streamingTime = (CompletionTime.Value - FirstTokenTime.Value).TotalSeconds;
            if (streamingTime <= 0)
                return null;

            return (TokenCount - 1) / streamingTime;
        }
    }

    /// <summary>
    /// Creates metrics indicating request was started.
    /// </summary>
    public static ResponseMetrics Started() => new()
    {
        RequestTime = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates metrics with first token time recorded.
    /// </summary>
    public ResponseMetrics WithFirstToken() => this with
    {
        FirstTokenTime = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates metrics with completion recorded.
    /// </summary>
    public ResponseMetrics WithCompletion(int tokenCount, long bytesReceived) => this with
    {
        CompletionTime = DateTimeOffset.UtcNow,
        TokenCount = tokenCount,
        BytesReceived = bytesReceived
    };

    /// <summary>
    /// Whether the response met the 2-second first token target.
    /// </summary>
    public bool MeetsFirstTokenTarget => TimeToFirstToken.HasValue
        && TimeToFirstToken.Value.TotalSeconds < 2.0;
}
