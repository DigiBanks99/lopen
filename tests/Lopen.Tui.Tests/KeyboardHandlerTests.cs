using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for KeyboardHandler, FocusPanel cycling, and context-aware hints.
/// Covers JOB-092 acceptance criteria.
/// </summary>
public class KeyboardHandlerTests
{
    private readonly KeyboardHandler _handler = new();

    // ==================== Ctrl+P Pause/Resume ====================

    [Theory]
    [InlineData(FocusPanel.Prompt)]
    [InlineData(FocusPanel.Activity)]
    [InlineData(FocusPanel.Context)]
    public void CtrlP_TogglesPause_AnyPanel(FocusPanel focus)
    {
        var input = new KeyInput { Key = ConsoleKey.P, Modifiers = ConsoleModifiers.Control };
        Assert.Equal(KeyAction.TogglePause, _handler.Handle(input, focus));
    }

    // ==================== Ctrl+C Cancel ====================

    [Theory]
    [InlineData(FocusPanel.Prompt)]
    [InlineData(FocusPanel.Activity)]
    [InlineData(FocusPanel.Context)]
    public void CtrlC_Cancels_AnyPanel(FocusPanel focus)
    {
        var input = new KeyInput { Key = ConsoleKey.C, Modifiers = ConsoleModifiers.Control };
        Assert.Equal(KeyAction.Cancel, _handler.Handle(input, focus));
    }

    // ==================== Tab Focus Cycling ====================

    [Fact]
    public void Tab_CyclesFocus()
    {
        var input = new KeyInput { Key = ConsoleKey.Tab };
        Assert.Equal(KeyAction.CycleFocusForward, _handler.Handle(input, FocusPanel.Prompt));
    }

    [Fact]
    public void CycleFocus_PromptToActivity()
    {
        Assert.Equal(FocusPanel.Activity, KeyboardHandler.CycleFocus(FocusPanel.Prompt));
    }

    [Fact]
    public void CycleFocus_ActivityToContext()
    {
        Assert.Equal(FocusPanel.Context, KeyboardHandler.CycleFocus(FocusPanel.Activity));
    }

    [Fact]
    public void CycleFocus_ContextToPrompt()
    {
        Assert.Equal(FocusPanel.Prompt, KeyboardHandler.CycleFocus(FocusPanel.Context));
    }

    // ==================== Enter / Alt+Enter ====================

    [Fact]
    public void Enter_InPrompt_SubmitsPrompt()
    {
        var input = new KeyInput { Key = ConsoleKey.Enter };
        Assert.Equal(KeyAction.SubmitPrompt, _handler.Handle(input, FocusPanel.Prompt));
    }

    [Fact]
    public void AltEnter_InPrompt_InsertsNewline()
    {
        var input = new KeyInput { Key = ConsoleKey.Enter, Modifiers = ConsoleModifiers.Alt };
        Assert.Equal(KeyAction.InsertNewline, _handler.Handle(input, FocusPanel.Prompt));
    }

    [Fact]
    public void Enter_OutsidePrompt_TogglesExpand()
    {
        var input = new KeyInput { Key = ConsoleKey.Enter };
        Assert.Equal(KeyAction.ToggleExpand, _handler.Handle(input, FocusPanel.Activity));
    }

    // ==================== Number Keys 1-9 Resource Access ====================

    [Theory]
    [InlineData(ConsoleKey.D1, KeyAction.ViewResource1)]
    [InlineData(ConsoleKey.D2, KeyAction.ViewResource2)]
    [InlineData(ConsoleKey.D3, KeyAction.ViewResource3)]
    [InlineData(ConsoleKey.D4, KeyAction.ViewResource4)]
    [InlineData(ConsoleKey.D5, KeyAction.ViewResource5)]
    [InlineData(ConsoleKey.D6, KeyAction.ViewResource6)]
    [InlineData(ConsoleKey.D7, KeyAction.ViewResource7)]
    [InlineData(ConsoleKey.D8, KeyAction.ViewResource8)]
    [InlineData(ConsoleKey.D9, KeyAction.ViewResource9)]
    public void NumberKeys_InActivityPanel_ViewsResource(ConsoleKey key, KeyAction expected)
    {
        var input = new KeyInput { Key = key };
        Assert.Equal(expected, _handler.Handle(input, FocusPanel.Activity));
    }

    [Fact]
    public void NumberKeys_InPrompt_DoNotTriggerResource()
    {
        var input = new KeyInput { Key = ConsoleKey.D1 };
        Assert.Equal(KeyAction.None, _handler.Handle(input, FocusPanel.Prompt));
    }

    [Fact]
    public void NumberKeys_InContextPanel_ViewsResource()
    {
        var input = new KeyInput { Key = ConsoleKey.D5 };
        Assert.Equal(KeyAction.ViewResource5, _handler.Handle(input, FocusPanel.Context));
    }

    // ==================== Expand/Collapse ====================

    [Fact]
    public void Space_InActivity_TogglesExpand()
    {
        var input = new KeyInput { Key = ConsoleKey.Spacebar };
        Assert.Equal(KeyAction.ToggleExpand, _handler.Handle(input, FocusPanel.Activity));
    }

    [Fact]
    public void Space_InPrompt_ReturnsNone()
    {
        var input = new KeyInput { Key = ConsoleKey.Spacebar };
        Assert.Equal(KeyAction.None, _handler.Handle(input, FocusPanel.Prompt));
    }

    // ==================== Context-Aware Hints ====================

    [Fact]
    public void Hints_InPrompt_ShowsEnterAndNewline()
    {
        var hints = KeyboardHandler.GetHints(FocusPanel.Prompt, isPaused: false);
        Assert.Contains("Enter: Submit", hints);
        Assert.Contains("Alt+Enter: Newline", hints);
        Assert.DoesNotContain("1-9: Resources", hints);
    }

    [Fact]
    public void Hints_InActivity_ShowsResourceAndExpand()
    {
        var hints = KeyboardHandler.GetHints(FocusPanel.Activity, isPaused: false);
        Assert.Contains("1-9: Resources", hints);
        Assert.Contains("Space: Expand", hints);
        Assert.DoesNotContain("Enter: Submit", hints);
    }

    [Fact]
    public void Hints_Paused_ShowsResume()
    {
        var hints = KeyboardHandler.GetHints(FocusPanel.Prompt, isPaused: true);
        Assert.Contains("Ctrl+P: Resume", hints);
        Assert.DoesNotContain("Ctrl+P: Pause", hints);
    }

    [Fact]
    public void Hints_NotPaused_ShowsPause()
    {
        var hints = KeyboardHandler.GetHints(FocusPanel.Prompt, isPaused: false);
        Assert.Contains("Ctrl+P: Pause", hints);
        Assert.DoesNotContain("Ctrl+P: Resume", hints);
    }

    [Fact]
    public void Hints_AlwaysHaveTabAndCancel()
    {
        var hints = KeyboardHandler.GetHints(FocusPanel.Context, isPaused: false);
        Assert.Contains("Tab: Focus", hints);
        Assert.Contains("Ctrl+C: Cancel", hints);
    }

    // ==================== Edge Cases ====================

    [Fact]
    public void UnmappedKey_ReturnsNone()
    {
        var input = new KeyInput { Key = ConsoleKey.F12 };
        Assert.Equal(KeyAction.None, _handler.Handle(input, FocusPanel.Prompt));
    }

    [Fact]
    public void CtrlTab_DoesNotCycleFocus()
    {
        var input = new KeyInput { Key = ConsoleKey.Tab, Modifiers = ConsoleModifiers.Control };
        // Ctrl+C takes priority check won't match Tab due to HasCtrl, let's see
        Assert.Equal(KeyAction.None, _handler.Handle(input, FocusPanel.Prompt));
    }

    // ==================== Backspace ====================

    [Fact]
    public void Backspace_WhenPromptFocused_ReturnsBackspace()
    {
        var input = new KeyInput { Key = ConsoleKey.Backspace };
        Assert.Equal(KeyAction.Backspace, _handler.Handle(input, FocusPanel.Prompt));
    }

    [Fact]
    public void Backspace_WhenActivityFocused_ReturnsNone()
    {
        var input = new KeyInput { Key = ConsoleKey.Backspace };
        Assert.Equal(KeyAction.None, _handler.Handle(input, FocusPanel.Activity));
    }

    [Fact]
    public void Backspace_WhenContextFocused_ReturnsNone()
    {
        var input = new KeyInput { Key = ConsoleKey.Backspace };
        Assert.Equal(KeyAction.None, _handler.Handle(input, FocusPanel.Context));
    }
}
