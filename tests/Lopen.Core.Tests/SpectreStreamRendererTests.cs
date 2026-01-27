using Lopen.Core;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Lopen.Core.Tests;

public class SpectreStreamRendererTests
{
    [Fact]
    public async Task RenderStreamAsync_OutputsAllTokens()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Hello", " ", "World"));

        // Assert
        var output = console.Output;
        output.ShouldContain("Hello");
        output.ShouldContain("World");
    }

    [Fact]
    public async Task RenderStreamAsync_ShowsThinkingIndicator()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var config = new StreamConfig { ShowThinkingIndicator = true };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Test"), config);

        // Assert
        console.Output.ShouldContain("Thinking");
    }

    [Fact]
    public async Task RenderStreamAsync_NoThinkingIndicator_WhenDisabled()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var config = new StreamConfig { ShowThinkingIndicator = false };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Test"), config);

        // Assert
        console.Output.ShouldNotContain("Thinking");
    }

    [Fact]
    public async Task RenderStreamAsync_RendersCodeBlocks()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("```csharp\nvar x = 1;\n```"));

        // Assert
        var output = console.Output;
        output.ShouldContain("var x = 1");
    }

    [Fact]
    public async Task RenderStreamAsync_RendersCodeBlockLanguage()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("```python\nprint('hello')\n```"));

        // Assert
        var output = console.Output;
        output.ShouldContain("python");
        output.ShouldContain("print");
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesOnParagraphBreak()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var fakeTime = new FakeTimeProvider();
        var renderer = new SpectreStreamRenderer(console, fakeTime);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("First paragraph", "\n\n", "Second paragraph"));

        // Assert
        var output = console.Output;
        output.ShouldContain("First paragraph");
        output.ShouldContain("Second paragraph");
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesOnTimeout()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var fakeTime = new FakeTimeProvider();
        var renderer = new SpectreStreamRenderer(console, fakeTime);
        var config = new StreamConfig { FlushTimeoutMs = 100 };

        // Act
        await renderer.RenderStreamAsync(CreateTimedTokenStream(fakeTime, 150), config);

        // Assert
        console.Output.ShouldContain("Token");
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesOnTokenLimit()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var config = new StreamConfig { MaxTokensBeforeFlush = 5 };
        var tokens = Enumerable.Range(0, 10).Select(i => $"T{i}").ToArray();

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream(tokens), config);

        // Assert
        var output = console.Output;
        output.ShouldContain("T0");
        output.ShouldContain("T9");
    }

    [Fact]
    public async Task RenderStreamAsync_WaitsForCompleteCodeBlock()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var config = new StreamConfig { MaxTokensBeforeFlush = 2 };

        // Act - tokens include code block that spans many tokens
        await renderer.RenderStreamAsync(CreateTokenStream("```", "line1\n", "line2\n", "```"));

        // Assert - code should be complete
        var output = console.Output;
        output.ShouldContain("line1");
        output.ShouldContain("line2");
    }

    [Fact]
    public async Task RenderStreamAsync_HandlesEmptyStream()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream());

        // Assert - should not throw, just show thinking indicator
        console.Output.ShouldContain("Thinking");
    }

    [Fact]
    public async Task RenderStreamAsync_HandlesCancellation()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var cts = new CancellationTokenSource();

        // Cancel after first few tokens are consumed
        cts.CancelAfter(50);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await renderer.RenderStreamAsync(CreateSlowTokenStream(), cancellationToken: cts.Token);
        });

        // Should have some partial content
        console.Output.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RenderStreamAsync_EscapesMarkup()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("[red]not markup[/]"));

        // Assert - should be escaped, not rendered as red
        console.Output.ShouldContain("[red]not markup[/]");
    }

    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SpectreStreamRenderer(null!));
    }

    [Fact]
    public async Task RenderStreamAsync_MixedContentAndCodeBlocks()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream(
            "Here's some code:\n",
            "```python\n",
            "print('hello')\n",
            "```\n",
            "And more text."
        ));

        // Assert
        var output = console.Output;
        output.ShouldContain("Here's some code");
        output.ShouldContain("print");
        output.ShouldContain("And more text");
    }

    [Fact]
    public void FakeTimeProvider_AdvancesTime()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var initial = fakeTime.UtcNow;

        // Act
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));

        // Assert
        (fakeTime.UtcNow - initial).TotalMilliseconds.ShouldBe(500);
    }

    [Fact]
    public void FakeTimeProvider_SetsTime()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var target = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        fakeTime.SetTime(target);

        // Assert
        fakeTime.UtcNow.ShouldBe(target);
    }

    [Fact]
    public async Task RenderStreamAsync_CollectsMetrics_WhenConfigured()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var metrics = new MockMetricsCollector();
        var config = new StreamConfig
        {
            ShowThinkingIndicator = false,
            MetricsCollector = metrics
        };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Hello", " ", "World"), config);

        // Assert
        metrics.StartRequestCount.ShouldBe(1);
        metrics.FirstTokenCount.ShouldBe(1);
        metrics.CompletionCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderStreamAsync_RecordsFirstTokenOnce()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var metrics = new MockMetricsCollector();
        var config = new StreamConfig
        {
            ShowThinkingIndicator = false,
            MetricsCollector = metrics
        };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("A", "B", "C", "D", "E"), config);

        // Assert
        metrics.FirstTokenCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderStreamAsync_RecordsTokenCountAndBytes()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var metrics = new MetricsCollector();
        var config = new StreamConfig
        {
            ShowThinkingIndicator = false,
            MetricsCollector = metrics
        };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Hello", " ", "World"), config);

        // Assert
        var result = metrics.GetLatestMetrics();
        result.ShouldNotBeNull();
        result.TokenCount.ShouldBe(3);
        result.BytesReceived.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderStreamAsync_RecordsMetricsOnCancellation()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var metrics = new MockMetricsCollector();
        var config = new StreamConfig
        {
            ShowThinkingIndicator = false,
            MetricsCollector = metrics
        };
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await renderer.RenderStreamAsync(CreateSlowTokenStream(), config, cts.Token);
        });

        // Metrics should still be recorded
        metrics.CompletionCount.ShouldBe(1);
    }

    [Fact]
    public async Task RenderStreamAsync_ShowsMetrics_WhenEnabled()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var metrics = new MetricsCollector();
        var config = new StreamConfig
        {
            ShowThinkingIndicator = false,
            MetricsCollector = metrics,
            ShowMetrics = true
        };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Test"), config);

        // Assert
        console.Output.ShouldContain("Metrics");
        console.Output.ShouldContain("Time to first token");
    }

    [Fact]
    public async Task RenderStreamAsync_HidesMetrics_WhenDisabled()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var metrics = new MetricsCollector();
        var config = new StreamConfig
        {
            ShowThinkingIndicator = false,
            MetricsCollector = metrics,
            ShowMetrics = false
        };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Test"), config);

        // Assert
        console.Output.ShouldNotContain("Metrics");
    }

    private static async IAsyncEnumerable<string> CreateTokenStream(params string[] tokens)
    {
        foreach (var token in tokens)
        {
            yield return token;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> CreateTimedTokenStream(FakeTimeProvider fakeTime, int advanceMs)
    {
        yield return "Token";
        fakeTime.Advance(TimeSpan.FromMilliseconds(advanceMs));
        yield return " after delay";
        await Task.Yield();
    }

    private static async IAsyncEnumerable<string> CreateCancellableStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return "Partial";
        yield return " content";
        cancellationToken.ThrowIfCancellationRequested();
        yield return " never reached";
        await Task.Yield();
    }

    private static async IAsyncEnumerable<string> CreateSlowTokenStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return $"Token{i} ";
            await Task.Delay(20, cancellationToken);
        }
    }

    #region RenderStreamWithLiveLayoutAsync Tests

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_ReturnsFullContent()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();

        // Act
        var result = await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Hello", " ", "World"),
            layoutContext);

        // Assert
        result.ShouldBe("Hello World");
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_UpdatesLiveContext()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Hello", " ", "World"),
            layoutContext);

        // Assert - Should have updated main content at least once
        layoutContext.MainUpdates.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_RefreshesContext()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Hello", " ", "World"),
            layoutContext);

        // Assert - Should have called Refresh
        layoutContext.RefreshCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_ShowsThinkingIndicator()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();
        var config = new StreamConfig { ShowThinkingIndicator = true };

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Test"),
            layoutContext,
            config);

        // Assert - First update should contain thinking indicator
        // (before first token, it shows thinking, then content replaces it)
        layoutContext.MainUpdates.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_ThrowsOnNullContext()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await renderer.RenderStreamWithLiveLayoutAsync(
                CreateTokenStream("Test"),
                null!));
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_HandlesCodeBlocks()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();

        // Act
        var result = await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("```csharp\n", "var x = 1;\n", "```"),
            layoutContext);

        // Assert
        result.ShouldContain("var x = 1");
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_RecordsMetrics()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();
        var metricsCollector = new MetricsCollector();
        var config = new StreamConfig { MetricsCollector = metricsCollector };

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Hello", " ", "World"),
            layoutContext,
            config);

        // Assert
        var metrics = metricsCollector.GetLatestMetrics();
        metrics.ShouldNotBeNull();
        metrics.TokenCount.ShouldBe(3);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_HandlesCancellation()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreStreamRenderer(console);
        var layoutContext = new MockLiveLayoutContext();
        using var cts = new CancellationTokenSource();

        // Act & Assert
        await cts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await renderer.RenderStreamWithLiveLayoutAsync(
                CreateSlowTokenStream(cts.Token),
                layoutContext,
                cancellationToken: cts.Token));
    }

    #endregion
}
