using Spectre.Console;

namespace Lopen.Tui.Tests;

public class ResponseRendererTests
{
    [Fact]
    public void RenderContent_EmptyString_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        renderer.RenderContent(""); // Should not throw
    }

    [Fact]
    public void RenderContent_PlainText_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        renderer.RenderContent("Hello, world!");
    }

    [Fact]
    public void RenderContent_CodeBlock_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        renderer.RenderContent("```csharp\nvar x = 1;\n```");
    }

    [Fact]
    public void RenderContent_BulletList_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        renderer.RenderContent("- Item one\n- Item two\n* Item three");
    }

    [Fact]
    public void RenderContent_MixedContent_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        renderer.RenderContent("Some text\n```\ncode here\n```\n- A bullet\n**Bold text**");
    }

    [Fact]
    public void RenderContent_SpecialCharacters_DoesNotThrow()
    {
        var renderer = CreateRenderer();
        renderer.RenderContent("Text with [brackets] and {braces}");
    }

    // ── Inline Markdown Tests ────────────────────────────────────────

    [Fact]
    public void RenderInlineMarkdown_BoldText_WrapsBold()
    {
        var result = ResponseRenderer.RenderInlineMarkdown("**important**");
        Assert.Contains("[bold]", result);
        Assert.Contains("important", result);
    }

    [Fact]
    public void RenderInlineMarkdown_PlainText_EscapesSpecialChars()
    {
        var result = ResponseRenderer.RenderInlineMarkdown("[test]");
        Assert.Contains("[[test]]", result); // Escaped brackets
    }

    [Fact]
    public void RenderInlineMarkdown_EmptyString_ReturnsEmpty()
    {
        var result = ResponseRenderer.RenderInlineMarkdown("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RenderInlineMarkdown_MixedBoldAndPlain()
    {
        var result = ResponseRenderer.RenderInlineMarkdown("Hello **world** here");
        Assert.Contains("[bold]", result);
        Assert.Contains("world", result);
        Assert.Contains("Hello", result);
    }

    private static ResponseRenderer CreateRenderer()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        return new ResponseRenderer(console);
    }
}
