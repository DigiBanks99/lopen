using Spectre.Console;

namespace Lopen.Tui.Tests;

public class ToolCallRendererTests
{
    [Theory]
    [InlineData(0.5, "500ms")]
    [InlineData(1.0, "1s")]
    [InlineData(1.5, "1.5s")]
    [InlineData(10.2, "10.2s")]
    public void FormatDuration_FormatsCorrectly(double seconds, string expected)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        Assert.Equal(expected, ToolCallRenderer.FormatDuration(duration));
    }

    [Fact]
    public void RenderSuccess_DoesNotThrow()
    {
        ToolCallRenderer renderer = CreateRenderer();
        renderer.RenderSuccess("git-diff", TimeSpan.FromSeconds(1.2));
    }

    [Fact]
    public void RenderSuccess_WithOutput_DoesNotThrow()
    {
        ToolCallRenderer renderer = CreateRenderer();
        renderer.RenderSuccess("git-diff", TimeSpan.FromSeconds(0.5), "diff output here");
    }

    [Fact]
    public void RenderFailure_DoesNotThrow()
    {
        ToolCallRenderer renderer = CreateRenderer();
        renderer.RenderFailure("git-commit", "file not found", TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void RenderSuccess_SpecialCharsInOutput_DoesNotThrow()
    {
        ToolCallRenderer renderer = CreateRenderer();
        renderer.RenderSuccess("tool", TimeSpan.FromSeconds(1), "output with [brackets] and {braces}");
    }

    private static ToolCallRenderer CreateRenderer()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        return new ToolCallRenderer(console);
    }
}
