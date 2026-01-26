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
}
