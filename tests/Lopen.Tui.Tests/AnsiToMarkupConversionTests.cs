namespace Lopen.Tui.Tests;

public class AnsiToMarkupConversionTests
{
    [Fact]
    public void PlainText_NoAnsiCodes()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("hello world");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void EmptyString()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NullString()
    {
        var result = TuiApplication.ConvertAnsiToMarkup(null!);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SingleRedColorCode()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("\x1b[31merror\x1b[0m");

        Assert.Equal("[red]error[/]", result);
    }

    [Fact]
    public void GreenAndResetCodes()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("\x1b[32m+ added line\x1b[0m");

        Assert.Equal("[green]+ added line[/]", result);
    }

    [Fact]
    public void BoldWithReset()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("\x1b[1mpath.cs\x1b[0m");

        Assert.Equal("[bold]path.cs[/]", result);
    }

    [Fact]
    public void MultipleColorSegments()
    {
        var result = TuiApplication.ConvertAnsiToMarkup(
            "\x1b[1mfile.cs\x1b[0m (\x1b[32m+3\x1b[0m \x1b[31m-1\x1b[0m)");

        Assert.Equal("[bold]file.cs[/] ([green]+3[/] [red]-1[/])", result);
    }

    [Fact]
    public void NestedAnsiCodes_ResetClosesAll()
    {
        var result = TuiApplication.ConvertAnsiToMarkup(
            "\x1b[32m+\x1b[34mkeyword\x1b[0m rest");

        Assert.Equal("[green]+[blue]keyword[/][/] rest", result);
    }

    [Fact]
    public void LiteralBracketsEscaped()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("array[0]");

        Assert.Equal("array[[0]]", result);
    }

    [Fact]
    public void LiteralBracketsEscaped_WithAnsiCodes()
    {
        var result = TuiApplication.ConvertAnsiToMarkup(
            "\x1b[31merror in arr[0]\x1b[0m");

        Assert.Equal("[red]error in arr[[0]][/]", result);
    }

    [Fact]
    public void AllSupportedColorCodes()
    {
        Assert.Equal("[red]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[31mx\x1b[0m"));
        Assert.Equal("[green]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[32mx\x1b[0m"));
        Assert.Equal("[yellow]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[33mx\x1b[0m"));
        Assert.Equal("[blue]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[34mx\x1b[0m"));
        Assert.Equal("[magenta]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[35mx\x1b[0m"));
        Assert.Equal("[cyan]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[36mx\x1b[0m"));
        Assert.Equal("[grey]x[/]", TuiApplication.ConvertAnsiToMarkup("\x1b[90mx\x1b[0m"));
    }

    [Fact]
    public void UnclosedAnsiTag_AutoClosed()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("\x1b[31munclosed");

        Assert.Equal("[red]unclosed[/]", result);
    }

    [Fact]
    public void UnknownAnsiCode_Stripped()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("\x1b[99mtext\x1b[0m");

        Assert.Equal("text", result);
    }

    [Fact]
    public void TextWithNoAnsi_BracketsOnly()
    {
        var result = TuiApplication.ConvertAnsiToMarkup("[Press Enter to view full document]");

        Assert.Equal("[[Press Enter to view full document]]", result);
    }

    [Fact]
    public void SyntaxHighlighterPattern_KeywordAndString()
    {
        var result = TuiApplication.ConvertAnsiToMarkup(
            "\x1b[34mpublic\x1b[0m void Get(\x1b[32m\"key\"\x1b[0m)");

        Assert.Equal("[blue]public[/] void Get([green]\"key\"[/])", result);
    }
}
