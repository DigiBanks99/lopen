namespace Lopen.Core;

/// <summary>
/// Mock metrics collector for testing.
/// </summary>
public class MockMetricsCollector : IMetricsCollector
{
    private readonly List<ResponseMetrics> _metrics = [];
    private ResponseMetrics? _current;

    /// <summary>Number of times StartRequest was called.</summary>
    public int StartRequestCount { get; private set; }

    /// <summary>Number of times RecordFirstToken was called.</summary>
    public int FirstTokenCount { get; private set; }

    /// <summary>Number of times RecordCompletion was called.</summary>
    public int CompletionCount { get; private set; }

    /// <summary>Whether to simulate meeting the 2s first token target.</summary>
    public bool SimulateFastResponse { get; set; } = true;

    /// <summary>Fixed time to first token for deterministic testing.</summary>
    public TimeSpan? FixedTimeToFirstToken { get; set; }

    /// <summary>Fixed total time for deterministic testing.</summary>
    public TimeSpan? FixedTotalTime { get; set; }

    /// <inheritdoc />
    public ResponseMetrics StartRequest(string? requestId = null)
    {
        StartRequestCount++;
        _current = ResponseMetrics.Started();
        return _current;
    }

    /// <inheritdoc />
    public void RecordFirstToken(string? requestId = null)
    {
        FirstTokenCount++;
        if (_current != null && !_current.FirstTokenTime.HasValue)
        {
            if (FixedTimeToFirstToken.HasValue)
            {
                _current = _current with
                {
                    FirstTokenTime = _current.RequestTime + FixedTimeToFirstToken.Value
                };
            }
            else
            {
                _current = _current.WithFirstToken();
            }
        }
    }

    /// <inheritdoc />
    public void RecordCompletion(int tokenCount, long bytesReceived, string? requestId = null)
    {
        CompletionCount++;
        if (_current != null)
        {
            if (FixedTotalTime.HasValue)
            {
                _current = _current with
                {
                    CompletionTime = _current.RequestTime + FixedTotalTime.Value,
                    TokenCount = tokenCount,
                    BytesReceived = bytesReceived
                };
            }
            else
            {
                _current = _current.WithCompletion(tokenCount, bytesReceived);
            }
            _metrics.Add(_current);
        }
    }

    /// <inheritdoc />
    public ResponseMetrics? GetLatestMetrics() => _current;

    /// <inheritdoc />
    public ResponseMetrics? GetMetrics(string requestId) => _current;

    /// <inheritdoc />
    public IReadOnlyList<ResponseMetrics> GetAllMetrics() => _metrics.ToList();

    /// <inheritdoc />
    public void Clear()
    {
        _metrics.Clear();
        _current = null;
        StartRequestCount = 0;
        FirstTokenCount = 0;
        CompletionCount = 0;
    }
}
