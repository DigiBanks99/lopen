using Lopen.Core;
using Shouldly;

namespace Lopen.Core.Tests;

public class MockStreamRendererTests
{
    [Fact]
    public async Task RenderStreamAsync_RecordsAllTokens()
    {
        // Arrange
        var renderer = new MockStreamRenderer();

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Hello", " ", "World"));

        // Assert
        renderer.AllTokens.Count.ShouldBe(3);
        renderer.FullContent.ShouldBe("Hello World");
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesOnParagraphBreak()
    {
        // Arrange
        var renderer = new MockStreamRenderer();

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("First", "\n\n", "Second"));

        // Assert
        renderer.FlushEvents.Count.ShouldBeGreaterThanOrEqualTo(1);
        renderer.FlushEvents[0].Content.ShouldContain("First");
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesAtEndOfStream()
    {
        // Arrange
        var renderer = new MockStreamRenderer();

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Hello", " ", "World"));

        // Assert
        renderer.FlushEvents.Any(f => f.Reason == MockStreamRenderer.FlushReason.EndOfStream).ShouldBeTrue();
    }

    [Fact]
    public async Task RenderStreamAsync_ShowsThinkingIndicator()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var config = new StreamConfig { ShowThinkingIndicator = true };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Test"), config);

        // Assert
        renderer.ThinkingIndicatorShown.ShouldBeTrue();
    }

    [Fact]
    public async Task RenderStreamAsync_NoThinkingIndicator_WhenDisabled()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var config = new StreamConfig { ShowThinkingIndicator = false };

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("Test"), config);

        // Assert
        renderer.ThinkingIndicatorShown.ShouldBeFalse();
    }

    [Fact]
    public async Task RenderStreamAsync_TracksCodeBlocks()
    {
        // Arrange
        var renderer = new MockStreamRenderer();

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("```csharp\ncode\n```"));

        // Assert
        renderer.FlushEvents.Any(f => f.ContainsCodeBlock).ShouldBeTrue();
    }

    [Fact]
    public async Task RenderStreamAsync_HandlesCancellation()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var cts = new CancellationTokenSource();

        // Set up callback to cancel after receiving some tokens
        var tokenCount = 0;
        renderer.OnToken = _ =>
        {
            tokenCount++;
            if (tokenCount >= 2)
            {
                cts.Cancel();
            }
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await renderer.RenderStreamAsync(CreateLongTokenStream(), cancellationToken: cts.Token);
        });

        renderer.WasCancelled.ShouldBeTrue();
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesPartialOnCancellation()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var cts = new CancellationTokenSource();

        // Set up callback to cancel after receiving some tokens
        var tokenCount = 0;
        renderer.OnToken = _ =>
        {
            tokenCount++;
            if (tokenCount >= 2)
            {
                cts.Cancel();
            }
        };

        // Act
        try
        {
            await renderer.RenderStreamAsync(CreateLongTokenStream(), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException) { }

        // Assert
        renderer.FlushEvents.Any(f => f.Reason == MockStreamRenderer.FlushReason.Cancelled).ShouldBeTrue();
        renderer.FlushEvents.First(f => f.Reason == MockStreamRenderer.FlushReason.Cancelled).Content.ShouldEndWith("...");
    }

    [Fact]
    public async Task Reset_ClearsAllState()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        await renderer.RenderStreamAsync(CreateTokenStream("Test"));

        // Act
        renderer.Reset();

        // Assert
        renderer.AllTokens.Count.ShouldBe(0);
        renderer.FlushEvents.Count.ShouldBe(0);
        renderer.ThinkingIndicatorShown.ShouldBeFalse();
        renderer.WasCancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task RenderStreamAsync_InvokesOnTokenCallback()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var receivedTokens = new List<string>();
        renderer.OnToken = t => receivedTokens.Add(t);

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream("A", "B", "C"));

        // Assert
        receivedTokens.ShouldBe(new[] { "A", "B", "C" });
    }

    [Fact]
    public async Task RenderStreamAsync_FlushesOnTokenLimit()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var config = new StreamConfig { MaxTokensBeforeFlush = 3 };
        var tokens = Enumerable.Range(0, 10).Select(i => $"t{i}").ToArray();

        // Act
        await renderer.RenderStreamAsync(CreateTokenStream(tokens), config);

        // Assert
        // Should have multiple flushes due to token limit
        renderer.FlushEvents.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task RenderStreamAsync_DoesNotFlushMidCodeBlock()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var config = new StreamConfig { MaxTokensBeforeFlush = 2 };

        // Act - 5 tokens but inside code block
        await renderer.RenderStreamAsync(CreateTokenStream("```", "a", "b", "c", "```"));

        // Assert - Should flush at end with complete code block
        renderer.FlushEvents.Count.ShouldBe(1);
        renderer.FlushEvents[0].Content.ShouldContain("```a");
    }

    [Fact]
    public async Task DefaultConfig_HasCorrectValues()
    {
        // Arrange & Act
        var config = new StreamConfig();

        // Assert
        config.FlushTimeoutMs.ShouldBe(500);
        config.MaxTokensBeforeFlush.ShouldBe(100);
        config.ShowThinkingIndicator.ShouldBeTrue();
        config.ThinkingText.ShouldBe("⏳ Thinking...");
    }

    [Fact]
    public void StreamConfig_LiveContextAndPromptTextDefaults()
    {
        // Arrange & Act
        var config = new StreamConfig();

        // Assert
        config.LiveContext.ShouldBeNull();
        config.PromptText.ShouldBe("lopen> ");
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_RecordsLiveLayoutCall()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var layoutContext = new MockLiveLayoutContext();

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Hello", "World"),
            layoutContext);

        // Assert
        renderer.LiveLayoutCalls.Count.ShouldBe(1);
        renderer.LiveLayoutCalls[0].LayoutContext.ShouldBe(layoutContext);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_ReturnsFullContent()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var layoutContext = new MockLiveLayoutContext();

        // Act
        var result = await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Hello", " ", "World"),
            layoutContext);

        // Assert
        result.ShouldBe("Hello World");
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_SetsLastLiveContext()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var layoutContext = new MockLiveLayoutContext();

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Test"),
            layoutContext);

        // Assert
        renderer.LastLiveLayoutContext.ShouldBe(layoutContext);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_SimulatesMainUpdates()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var layoutContext = new MockLiveLayoutContext();

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("A", "B", "C"),
            layoutContext);

        // Assert - Should have simulated one update per token
        layoutContext.SimulatedMainUpdateCount.ShouldBe(3);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_RecordsAllTokens()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        var layoutContext = new MockLiveLayoutContext();

        // Act
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("A", "B", "C"),
            layoutContext);

        // Assert
        renderer.AllTokens.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RenderStreamWithLiveLayoutAsync_ThrowsOnNullContext()
    {
        // Arrange
        var renderer = new MockStreamRenderer();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await renderer.RenderStreamWithLiveLayoutAsync(
                CreateTokenStream("Test"),
                null!));
    }

    [Fact]
    public async Task Reset_ClearsLiveLayoutData()
    {
        // Arrange
        var renderer = new MockStreamRenderer();
        await renderer.RenderStreamWithLiveLayoutAsync(
            CreateTokenStream("Test"),
            new MockLiveLayoutContext());

        // Act
        renderer.Reset();

        // Assert
        renderer.LiveLayoutCalls.Count.ShouldBe(0);
        renderer.LastLiveLayoutContext.ShouldBeNull();
    }

    private static async IAsyncEnumerable<string> CreateTokenStream(params string[] tokens)
    {
        foreach (var token in tokens)
        {
            yield return token;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> CreateLongTokenStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tokens = new[] { "Token1", "Token2", "Token3", "Token4", "Token5" };
        foreach (var token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token;
            await Task.Yield();
        }
    }
}
