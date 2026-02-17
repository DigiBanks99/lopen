namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for SyntaxHighlighter language-aware code highlighting (TUI-15).
/// </summary>
public class SyntaxHighlighterTests
{
    [Fact]
    public void HighlightLine_CSharpKeyword_WrapsInBlue()
    {
        var result = SyntaxHighlighter.HighlightLine("public class Foo", ".cs");

        Assert.Contains("\x1b[34mpublic\x1b[0m", result);
        Assert.Contains("\x1b[34mclass\x1b[0m", result);
    }

    [Fact]
    public void HighlightLine_CSharpNonKeyword_NotColored()
    {
        var result = SyntaxHighlighter.HighlightLine("Foo bar baz", ".cs");

        Assert.DoesNotContain("\x1b[34m", result);
    }

    [Fact]
    public void HighlightLine_PythonKeyword_WrapsInBlue()
    {
        var result = SyntaxHighlighter.HighlightLine("def foo():", ".py");

        Assert.Contains("\x1b[34mdef\x1b[0m", result);
    }

    [Fact]
    public void HighlightLine_TypeScriptKeyword_WrapsInBlue()
    {
        var result = SyntaxHighlighter.HighlightLine("const x = 5;", ".ts");

        Assert.Contains("\x1b[34mconst\x1b[0m", result);
    }

    [Fact]
    public void HighlightLine_UnknownExtension_ReturnsUnchanged()
    {
        var result = SyntaxHighlighter.HighlightLine("some text", ".xyz");

        Assert.Equal("some text", result);
    }

    [Fact]
    public void HighlightLine_NullExtension_ReturnsUnchanged()
    {
        var result = SyntaxHighlighter.HighlightLine("some text", null);

        Assert.Equal("some text", result);
    }

    [Fact]
    public void SupportsExtension_CSharp_True()
    {
        Assert.True(SyntaxHighlighter.SupportsExtension(".cs"));
    }

    [Fact]
    public void SupportsExtension_Unknown_False()
    {
        Assert.False(SyntaxHighlighter.SupportsExtension(".xyz"));
    }

    [Fact]
    public void SupportsExtension_CaseInsensitive()
    {
        Assert.True(SyntaxHighlighter.SupportsExtension(".CS"));
        Assert.True(SyntaxHighlighter.SupportsExtension(".Py"));
    }

    [Theory]
    [InlineData(".cs")]
    [InlineData(".ts")]
    [InlineData(".js")]
    [InlineData(".py")]
    public void HighlightLine_AllSupportedLanguages_ProducesOutput(string ext)
    {
        var result = SyntaxHighlighter.HighlightLine("return null;", ext);

        Assert.NotNull(result);
        Assert.Contains("\x1b[34mreturn\x1b[0m", result);
    }
}
