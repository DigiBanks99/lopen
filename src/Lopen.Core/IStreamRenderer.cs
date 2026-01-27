namespace Lopen.Core;

/// <summary>
/// Configuration for stream rendering.
/// </summary>
public record StreamConfig
{
    /// <summary>Maximum milliseconds to wait before flushing buffer. Default is 500ms.</summary>
    public int FlushTimeoutMs { get; init; } = 500;

    /// <summary>Maximum tokens to buffer before forcing flush. Default is 100.</summary>
    public int MaxTokensBeforeFlush { get; init; } = 100;

    /// <summary>Whether to show "Thinking..." indicator initially.</summary>
    public bool ShowThinkingIndicator { get; init; } = true;

    /// <summary>Text to show while waiting for first token.</summary>
    public string ThinkingText { get; init; } = "⏳ Thinking...";

    /// <summary>Optional metrics collector for timing data.</summary>
    public IMetricsCollector? MetricsCollector { get; init; }

    /// <summary>Whether to display metrics summary after stream completes.</summary>
    public bool ShowMetrics { get; init; }

    /// <summary>
    /// Optional live layout context for maintaining prompt position during streaming.
    /// When provided, streaming updates go to the main area of the live layout.
    /// </summary>
    public ILiveLayoutContext? LiveContext { get; init; }

    /// <summary>
    /// When using LiveContext, the prompt text to show in the footer.
    /// Default is "lopen> ".
    /// </summary>
    public string PromptText { get; init; } = "lopen> ";
}

/// <summary>
/// Renderer for streaming AI responses with buffered paragraph rendering.
/// </summary>
public interface IStreamRenderer
{
    /// <summary>
    /// Render a token stream with buffering for readability.
    /// Flushes on paragraph breaks, timeout, or token limit.
    /// </summary>
    /// <param name="tokenStream">Async enumerable of tokens.</param>
    /// <param name="config">Stream configuration (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RenderStreamAsync(
        IAsyncEnumerable<string> tokenStream,
        StreamConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Render a token stream using a live layout context for prompt position maintenance.
    /// The streaming content appears in the main area while the prompt stays visible.
    /// </summary>
    /// <param name="tokenStream">Async enumerable of tokens.</param>
    /// <param name="layoutContext">Live layout context to use for rendering.</param>
    /// <param name="config">Stream configuration (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete streamed response text.</returns>
    Task<string> RenderStreamWithLiveLayoutAsync(
        IAsyncEnumerable<string> tokenStream,
        ILiveLayoutContext layoutContext,
        StreamConfig? config = null,
        CancellationToken cancellationToken = default);
}
