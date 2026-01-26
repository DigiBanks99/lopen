using System.Collections.Concurrent;

namespace Lopen.Core;

/// <summary>
/// Collects and stores response metrics in memory.
/// </summary>
public class MetricsCollector : IMetricsCollector
{
    private const string DefaultRequestId = "__latest__";
    private readonly ConcurrentDictionary<string, ResponseMetrics> _metrics = new();
    private readonly List<ResponseMetrics> _history = [];
    private readonly object _historyLock = new();

    /// <inheritdoc />
    public ResponseMetrics StartRequest(string? requestId = null)
    {
        var id = requestId ?? DefaultRequestId;
        var metrics = ResponseMetrics.Started();
        _metrics[id] = metrics;
        return metrics;
    }

    /// <inheritdoc />
    public void RecordFirstToken(string? requestId = null)
    {
        var id = requestId ?? DefaultRequestId;
        if (_metrics.TryGetValue(id, out var current))
        {
            // Only record first token once
            if (!current.FirstTokenTime.HasValue)
            {
                _metrics[id] = current.WithFirstToken();
            }
        }
    }

    /// <inheritdoc />
    public void RecordCompletion(int tokenCount, long bytesReceived, string? requestId = null)
    {
        var id = requestId ?? DefaultRequestId;
        if (_metrics.TryGetValue(id, out var current))
        {
            var completed = current.WithCompletion(tokenCount, bytesReceived);
            _metrics[id] = completed;

            lock (_historyLock)
            {
                _history.Add(completed);
            }
        }
    }

    /// <inheritdoc />
    public ResponseMetrics? GetLatestMetrics()
    {
        _metrics.TryGetValue(DefaultRequestId, out var metrics);
        return metrics;
    }

    /// <inheritdoc />
    public ResponseMetrics? GetMetrics(string requestId)
    {
        _metrics.TryGetValue(requestId, out var metrics);
        return metrics;
    }

    /// <inheritdoc />
    public IReadOnlyList<ResponseMetrics> GetAllMetrics()
    {
        lock (_historyLock)
        {
            return _history.ToList();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _metrics.Clear();
        lock (_historyLock)
        {
            _history.Clear();
        }
    }
}
