using System.Text;
using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of stream renderer.
/// Buffers tokens into paragraphs for readable display.
/// Respects NO_COLOR environment variable.
/// </summary>
public class SpectreStreamRenderer : IStreamRenderer
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;
    private readonly ITimeProvider _timeProvider;

    public SpectreStreamRenderer()
        : this(AnsiConsole.Console)
    {
    }

    public SpectreStreamRenderer(IAnsiConsole console)
        : this(console, new SystemTimeProvider())
    {
    }

    public SpectreStreamRenderer(IAnsiConsole console, ITimeProvider timeProvider)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    /// <inheritdoc />
    public async Task RenderStreamAsync(
        IAsyncEnumerable<string> tokenStream,
        StreamConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new StreamConfig();

        var buffer = new StringBuilder();
        var tokenCount = 0;
        var totalTokenCount = 0;
        long bytesReceived = 0;
        var lastFlush = _timeProvider.UtcNow;
        var firstToken = true;
        var inCodeBlock = false;

        // Start metrics collection if enabled
        config.MetricsCollector?.StartRequest();

        // Show initial indicator
        if (config.ShowThinkingIndicator)
        {
            WriteThinkingIndicator(config.ThinkingText);
        }

        try
        {
            await foreach (var token in tokenStream.WithCancellation(cancellationToken))
            {
                // Record first token timing
                if (firstToken)
                {
                    config.MetricsCollector?.RecordFirstToken();

                    if (config.ShowThinkingIndicator)
                    {
                        ClearThinkingIndicator();
                    }
                    firstToken = false;
                }

                buffer.Append(token);
                tokenCount++;
                totalTokenCount++;
                bytesReceived += Encoding.UTF8.GetByteCount(token);

                // Track code block state
                var codeBlockMarkers = CountOccurrences(buffer.ToString(), "```");
                inCodeBlock = codeBlockMarkers % 2 == 1;

                // Don't flush mid-code-block
                if (inCodeBlock)
                {
                    continue;
                }

                var timeSinceFlush = (_timeProvider.UtcNow - lastFlush).TotalMilliseconds;
                var shouldFlush =
                    // Paragraph break detected
                    token.Contains("\n\n") ||
                    // Timeout reached
                    timeSinceFlush > config.FlushTimeoutMs ||
                    // Too many tokens buffered
                    tokenCount >= config.MaxTokensBeforeFlush;

                if (shouldFlush && buffer.Length > 0)
                {
                    FlushBuffer(buffer.ToString());
                    buffer.Clear();
                    tokenCount = 0;
                    lastFlush = _timeProvider.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Record completion before rethrowing
            config.MetricsCollector?.RecordCompletion(totalTokenCount, bytesReceived);

            // Flush partial content on cancellation
            if (buffer.Length > 0)
            {
                FlushBuffer(buffer + "...");
            }
            throw;
        }

        // Final flush
        if (buffer.Length > 0)
        {
            FlushBuffer(buffer.ToString());
        }

        // Record completion and optionally display metrics
        config.MetricsCollector?.RecordCompletion(totalTokenCount, bytesReceived);

        if (config.ShowMetrics && config.MetricsCollector != null)
        {
            DisplayMetrics(config.MetricsCollector.GetLatestMetrics());
        }
    }

    private void DisplayMetrics(ResponseMetrics? metrics)
    {
        if (metrics == null) return;

        _console.WriteLine();
        if (_useColors)
        {
            var ttft = metrics.TimeToFirstToken?.TotalMilliseconds ?? 0;
            var ttftColor = metrics.MeetsFirstTokenTarget ? "green" : "yellow";
            var total = metrics.TotalTime?.TotalMilliseconds ?? 0;
            var tps = metrics.TokensPerSecond ?? 0;

            _console.MarkupLine($"[dim]─── Metrics ───[/]");
            _console.MarkupLine($"[dim]Time to first token:[/] [{ttftColor}]{ttft:F0}ms[/]");
            _console.MarkupLine($"[dim]Total time:[/] [blue]{total:F0}ms[/]");
            _console.MarkupLine($"[dim]Tokens:[/] {metrics.TokenCount} [dim]({tps:F1}/s)[/]");
        }
        else
        {
            var ttft = metrics.TimeToFirstToken?.TotalMilliseconds ?? 0;
            var total = metrics.TotalTime?.TotalMilliseconds ?? 0;
            var tps = metrics.TokensPerSecond ?? 0;

            _console.WriteLine("--- Metrics ---");
            _console.WriteLine($"Time to first token: {ttft:F0}ms");
            _console.WriteLine($"Total time: {total:F0}ms");
            _console.WriteLine($"Tokens: {metrics.TokenCount} ({tps:F1}/s)");
        }
    }

    private void WriteThinkingIndicator(string text)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[dim]{Markup.Escape(text)}[/]");
        }
        else
        {
            _console.WriteLine(text);
        }
    }

    private void ClearThinkingIndicator()
    {
        // Move cursor up and clear line
        // Note: This is a simplification - in production, you might use
        // Live display for proper clearing
    }

    private void FlushBuffer(string content)
    {
        // Check if content contains code blocks
        if (content.Contains("```"))
        {
            RenderWithCodeBlocks(content);
        }
        else
        {
            // Render as formatted text
            RenderFormattedText(content);
        }
    }

    private void RenderFormattedText(string content)
    {
        if (_useColors)
        {
            var formatted = FormatMarkdown(content);
            _console.Markup(formatted);
        }
        else
        {
            _console.Write(content);
        }
    }

    private void RenderWithCodeBlocks(string content)
    {
        var parts = content.Split("```");

        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 0)
            {
                // Regular text
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    RenderFormattedText(parts[i]);
                }
            }
            else
            {
                // Code block
                var lines = parts[i].Split('\n', 2);
                var language = lines[0].Trim();
                var code = lines.Length > 1 ? lines[1].TrimEnd() : "";

                if (_useColors && _console.Profile.Capabilities.Interactive)
                {
                    var panel = new Panel(Markup.Escape(code))
                    {
                        Header = string.IsNullOrEmpty(language)
                            ? null
                            : new PanelHeader($" {language} "),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Grey),
                        Padding = new Padding(1, 0, 1, 0)
                    };
                    _console.Write(panel);
                }
                else
                {
                    // Plain text fallback
                    if (!string.IsNullOrEmpty(language))
                    {
                        _console.WriteLine($"--- {language} ---");
                    }
                    _console.WriteLine(code);
                    _console.WriteLine("---");
                }
            }
        }
    }

    private static string FormatMarkdown(string content)
    {
        // Escape first to prevent markup injection
        var escaped = Markup.Escape(content);

        // Apply basic markdown formatting
        // Note: This is a simplified implementation
        // A full implementation would use proper markdown parsing

        // Bold: **text** → [bold]text[/]
        escaped = ApplyMarkdownPair(escaped, "**", "[bold]", "[/]");

        // Italic: *text* → [italic]text[/]
        escaped = ApplyMarkdownPair(escaped, "*", "[italic]", "[/]");

        // Inline code: `text` → [cyan]text[/]
        escaped = ApplyMarkdownPair(escaped, "`", "[cyan]", "[/]");

        return escaped;
    }

    private static string ApplyMarkdownPair(string text, string marker, string openTag, string closeTag)
    {
        var escapedMarker = Markup.Escape(marker);
        var result = new StringBuilder();
        var inTag = false;
        var i = 0;

        while (i < text.Length)
        {
            if (i + escapedMarker.Length <= text.Length &&
                text.Substring(i, escapedMarker.Length) == escapedMarker)
            {
                result.Append(inTag ? closeTag : openTag);
                inTag = !inTag;
                i += escapedMarker.Length;
            }
            else
            {
                result.Append(text[i]);
                i++;
            }
        }

        return result.ToString();
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
}

/// <summary>
/// Interface for time provider to enable testing.
/// </summary>
public interface ITimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>
/// System time provider using DateTime.UtcNow.
/// </summary>
public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Controllable time provider for testing.
/// </summary>
public class FakeTimeProvider : ITimeProvider
{
    private DateTime _utcNow = DateTime.UtcNow;

    public DateTime UtcNow => _utcNow;

    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }

    public void SetTime(DateTime time)
    {
        _utcNow = time;
    }
}
