namespace Lopen.Core;

/// <summary>
/// Collects and stores response metrics.
/// </summary>
public interface IMetricsCollector
{
    /// <summary>
    /// Start tracking a new request.
    /// </summary>
    /// <param name="requestId">Optional request identifier.</param>
    /// <returns>Initial metrics with request time recorded.</returns>
    ResponseMetrics StartRequest(string? requestId = null);

    /// <summary>
    /// Record first token received.
    /// </summary>
    /// <param name="requestId">Request identifier.</param>
    void RecordFirstToken(string? requestId = null);

    /// <summary>
    /// Record request completion.
    /// </summary>
    /// <param name="tokenCount">Number of tokens received.</param>
    /// <param name="bytesReceived">Total bytes received.</param>
    /// <param name="requestId">Request identifier.</param>
    void RecordCompletion(int tokenCount, long bytesReceived, string? requestId = null);

    /// <summary>
    /// Get metrics for the most recent request.
    /// </summary>
    ResponseMetrics? GetLatestMetrics();

    /// <summary>
    /// Get metrics for a specific request.
    /// </summary>
    /// <param name="requestId">Request identifier.</param>
    ResponseMetrics? GetMetrics(string requestId);

    /// <summary>
    /// Get all collected metrics.
    /// </summary>
    IReadOnlyList<ResponseMetrics> GetAllMetrics();

    /// <summary>
    /// Clear all collected metrics.
    /// </summary>
    void Clear();
}
