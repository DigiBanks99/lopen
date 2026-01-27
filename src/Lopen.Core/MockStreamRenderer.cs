namespace Lopen.Core;

/// <summary>
/// Mock stream renderer for testing. Records all flush events.
/// </summary>
public class MockStreamRenderer : IStreamRenderer
{
    private readonly List<FlushEvent> _flushEvents = new();
    private readonly List<string> _allTokens = new();
    private readonly List<LiveLayoutCall> _liveLayoutCalls = new();

    /// <summary>
    /// Gets all flush events that occurred during rendering.
    /// </summary>
    public IReadOnlyList<FlushEvent> FlushEvents => _flushEvents.AsReadOnly();

    /// <summary>
    /// Gets all tokens that were streamed.
    /// </summary>
    public IReadOnlyList<string> AllTokens => _allTokens.AsReadOnly();

    /// <summary>
    /// Gets the concatenated content of all tokens.
    /// </summary>
    public string FullContent => string.Join("", _allTokens);

    /// <summary>
    /// Gets whether the thinking indicator was shown.
    /// </summary>
    public bool ThinkingIndicatorShown { get; private set; }

    /// <summary>
    /// Gets whether the stream was cancelled.
    /// </summary>
    public bool WasCancelled { get; private set; }

    /// <summary>
    /// Optional action to invoke for each token (for testing timing).
    /// </summary>
    public Action<string>? OnToken { get; set; }

    /// <inheritdoc />
    public async Task RenderStreamAsync(
        IAsyncEnumerable<string> tokenStream,
        StreamConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new StreamConfig();

        var buffer = new System.Text.StringBuilder();
        var tokenCount = 0;
        var totalTokenCount = 0;
        long bytesReceived = 0;
        var lastFlush = DateTime.UtcNow;
        var firstToken = true;

        // Start metrics collection if enabled
        config.MetricsCollector?.StartRequest();

        if (config.ShowThinkingIndicator)
        {
            ThinkingIndicatorShown = true;
        }

        try
        {
            await foreach (var token in tokenStream.WithCancellation(cancellationToken))
            {
                // Record first token timing
                if (firstToken)
                {
                    config.MetricsCollector?.RecordFirstToken();
                    firstToken = false;
                }

                _allTokens.Add(token);
                OnToken?.Invoke(token);

                buffer.Append(token);
                tokenCount++;
                totalTokenCount++;
                bytesReceived += System.Text.Encoding.UTF8.GetByteCount(token);

                // Track code block state
                var inCodeBlock = CountOccurrences(buffer.ToString(), "```") % 2 == 1;
                if (inCodeBlock)
                {
                    continue;
                }

                var timeSinceFlush = (DateTime.UtcNow - lastFlush).TotalMilliseconds;
                var shouldFlush =
                    token.Contains("\n\n") ||
                    timeSinceFlush > config.FlushTimeoutMs ||
                    tokenCount >= config.MaxTokensBeforeFlush;

                if (shouldFlush && buffer.Length > 0)
                {
                    RecordFlush(buffer.ToString(), FlushReason.Normal);
                    buffer.Clear();
                    tokenCount = 0;
                    lastFlush = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            config.MetricsCollector?.RecordCompletion(totalTokenCount, bytesReceived);
            if (buffer.Length > 0)
            {
                RecordFlush(buffer + "...", FlushReason.Cancelled);
            }
            throw;
        }

        // Final flush
        if (buffer.Length > 0)
        {
            RecordFlush(buffer.ToString(), FlushReason.EndOfStream);
        }

        // Record completion
        config.MetricsCollector?.RecordCompletion(totalTokenCount, bytesReceived);
    }

    private void RecordFlush(string content, FlushReason reason)
    {
        _flushEvents.Add(new FlushEvent
        {
            Content = content,
            Reason = reason,
            Timestamp = DateTime.UtcNow,
            ContainsCodeBlock = content.Contains("```")
        });
    }

    /// <summary>
    /// Clears all recorded events.
    /// </summary>
    public void Reset()
    {
        _flushEvents.Clear();
        _allTokens.Clear();
        _liveLayoutCalls.Clear();
        ThinkingIndicatorShown = false;
        WasCancelled = false;
        LastLiveLayoutContext = null;
    }

    /// <inheritdoc />
    public async Task<string> RenderStreamWithLiveLayoutAsync(
        IAsyncEnumerable<string> tokenStream,
        ILiveLayoutContext layoutContext,
        StreamConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(layoutContext);
        config ??= new StreamConfig();

        _liveLayoutCalls.Add(new LiveLayoutCall
        {
            LayoutContext = layoutContext,
            Config = config
        });
        LastLiveLayoutContext = layoutContext;

        var fullContent = new System.Text.StringBuilder();

        if (config.ShowThinkingIndicator)
        {
            ThinkingIndicatorShown = true;
        }

        try
        {
            await foreach (var token in tokenStream.WithCancellation(cancellationToken))
            {
                _allTokens.Add(token);
                OnToken?.Invoke(token);
                fullContent.Append(token);

                // Simulate refresh on each token for testing
                if (layoutContext is MockLiveLayoutContext mockContext)
                {
                    mockContext.SimulateMainUpdate();
                }
            }
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            throw;
        }

        return fullContent.ToString();
    }

    /// <summary>
    /// Gets all live layout calls.
    /// </summary>
    public IReadOnlyList<LiveLayoutCall> LiveLayoutCalls => _liveLayoutCalls.AsReadOnly();

    /// <summary>
    /// Gets the last used live layout context.
    /// </summary>
    public ILiveLayoutContext? LastLiveLayoutContext { get; private set; }

    /// <summary>
    /// Record of a live layout stream call.
    /// </summary>
    public record LiveLayoutCall
    {
        public ILiveLayoutContext? LayoutContext { get; init; }
        public StreamConfig? Config { get; init; }
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Record of a buffer flush event.
    /// </summary>
    public record FlushEvent
    {
        public string Content { get; init; } = string.Empty;
        public FlushReason Reason { get; init; }
        public DateTime Timestamp { get; init; }
        public bool ContainsCodeBlock { get; init; }
    }

    /// <summary>
    /// Reason for buffer flush.
    /// </summary>
    public enum FlushReason
    {
        Normal,
        EndOfStream,
        Cancelled
    }
}
