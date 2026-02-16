using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for PromptAreaComponent rendering.
/// Covers AC: multi-line prompt area with keyboard hints at bottom.
/// </summary>
public class PromptAreaComponentTests
{
    private readonly PromptAreaComponent _component = new();

    // ==================== Component metadata ====================

    [Fact]
    public void Name_IsPromptArea()
    {
        Assert.Equal("PromptArea", _component.Name);
    }

    // ==================== Placeholder ====================

    [Fact]
    public void Render_EmptyText_ShowsPlaceholder()
    {
        var data = new PromptAreaData();
        var lines = _component.Render(data, new ScreenRect(0, 0, 80, 3));

        Assert.Contains("> Your prompt here", lines[0]);
    }

    // ==================== Text input ====================

    [Fact]
    public void Render_WithText_ShowsTextWithPromptPrefix()
    {
        var data = new PromptAreaData { Text = "Build an auth module" };
        var lines = _component.Render(data, new ScreenRect(0, 0, 80, 3));

        Assert.Contains("> Build an auth module", lines[0]);
    }

    [Fact]
    public void Render_MultiLineText_ShowsMultipleLines()
    {
        var data = new PromptAreaData { Text = "Line one\nLine two" };
        var lines = _component.Render(data, new ScreenRect(0, 0, 80, 4));

        Assert.Contains("> Line one", lines[0]);
        Assert.Contains("Line two", lines[1]);
    }

    // ==================== Keyboard hints ====================

    [Fact]
    public void Render_ShowsKeyboardHints()
    {
        var data = new PromptAreaData();
        var lines = _component.Render(data, new ScreenRect(0, 0, 100, 3));

        var hintsLine = lines[^1];
        Assert.Contains("Enter: Submit", hintsLine);
        Assert.Contains("Alt+Enter: New line", hintsLine);
        Assert.Contains("Ctrl+P: Pause", hintsLine);
    }

    [Fact]
    public void Render_Paused_ShowsResumeHint()
    {
        var data = new PromptAreaData { IsPaused = true };
        var lines = _component.Render(data, new ScreenRect(0, 0, 100, 3));

        var hintsLine = lines[^1];
        Assert.Contains("Ctrl+P: Resume", hintsLine);
        Assert.DoesNotContain("Ctrl+P: Pause", hintsLine);
    }

    [Fact]
    public void Render_CustomHints_UsesCustom()
    {
        var data = new PromptAreaData { CustomHints = ["A: Do X", "B: Do Y"] };
        var lines = _component.Render(data, new ScreenRect(0, 0, 80, 3));

        var hintsLine = lines[^1];
        Assert.Contains("A: Do X", hintsLine);
        Assert.Contains("B: Do Y", hintsLine);
    }

    // ==================== BuildHintsLine ====================

    [Fact]
    public void BuildHintsLine_JoinsWithSeparator()
    {
        var hints = new[] { "A", "B", "C" };
        var result = PromptAreaComponent.BuildHintsLine(hints, false);
        Assert.Equal("A │ B │ C", result);
    }

    [Fact]
    public void BuildHintsLine_Paused_ReplacesCtrlP()
    {
        var result = PromptAreaComponent.BuildHintsLine(PromptAreaComponent.DefaultHints, true);
        Assert.Contains("Ctrl+P: Resume", result);
        Assert.DoesNotContain("Ctrl+P: Pause", result);
    }

    // ==================== WrapText ====================

    [Fact]
    public void WrapText_ShortText_SingleLine()
    {
        var result = PromptAreaComponent.WrapText("Hello", 80);
        Assert.Single(result);
        Assert.Equal("Hello", result[0]);
    }

    [Fact]
    public void WrapText_LongText_WrapsToWidth()
    {
        var text = new string('x', 20);
        var result = PromptAreaComponent.WrapText(text, 10);
        Assert.Equal(2, result.Count);
        Assert.Equal(10, result[0].Length);
        Assert.Equal(10, result[1].Length);
    }

    [Fact]
    public void WrapText_NewlineCharacters_SplitsOnNewline()
    {
        var result = PromptAreaComponent.WrapText("Line1\nLine2\nLine3", 80);
        Assert.Equal(3, result.Count);
    }

    // ==================== Edge cases ====================

    [Fact]
    public void Render_ZeroWidth_ReturnsEmpty()
    {
        Assert.Empty(_component.Render(new PromptAreaData(), new ScreenRect(0, 0, 0, 3)));
    }

    [Fact]
    public void Render_ZeroHeight_ReturnsEmpty()
    {
        Assert.Empty(_component.Render(new PromptAreaData(), new ScreenRect(0, 0, 80, 0)));
    }

    [Fact]
    public void Render_Height1_ShowsInputNoHints()
    {
        var data = new PromptAreaData { Text = "Test" };
        var lines = _component.Render(data, new ScreenRect(0, 0, 80, 1));

        Assert.Single(lines);
        Assert.Contains("> Test", lines[0]);
    }

    [Fact]
    public void Render_AllLinesSameWidth()
    {
        var data = new PromptAreaData { Text = "Short input" };
        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 3));

        foreach (var line in lines)
        {
            Assert.Equal(60, line.Length);
        }
    }

    // ==================== Spinner integration (JOB-057 / TUI-28) ====================

    [Fact]
    public void Render_WithSpinner_ShowsSpinnerInsteadOfInput()
    {
        var data = new PromptAreaData
        {
            Text = "Some input",
            Spinner = new SpinnerData { Message = "Processing...", Frame = 0 }
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 3));
        Assert.Contains(lines, l => l.Contains("Processing..."));
        Assert.DoesNotContain(lines, l => l.Contains("Some input"));
    }

    [Fact]
    public void Render_WithSpinner_IncludesSpinnerFrame()
    {
        var data = new PromptAreaData
        {
            Spinner = new SpinnerData { Message = "Loading", Frame = 2 }
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 3));
        Assert.Contains(lines, l => l.Contains(SpinnerComponent.Frames[2]));
    }

    [Fact]
    public void Render_NullSpinner_ShowsNormalInput()
    {
        var data = new PromptAreaData { Text = "Hello" };
        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 3));
        Assert.Contains(lines, l => l.Contains("Hello"));
    }
}
