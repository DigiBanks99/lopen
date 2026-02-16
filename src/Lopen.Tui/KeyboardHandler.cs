namespace Lopen.Tui;

/// <summary>
/// Represents a recognized key input event with modifiers.
/// </summary>
public sealed record KeyInput
{
    /// <summary>The key that was pressed.</summary>
    public required ConsoleKey Key { get; init; }

    /// <summary>Modifier keys held down.</summary>
    public ConsoleModifiers Modifiers { get; init; }

    /// <summary>Character representation, if any.</summary>
    public char KeyChar { get; init; }

    public bool HasCtrl => (Modifiers & ConsoleModifiers.Control) != 0;
    public bool HasAlt => (Modifiers & ConsoleModifiers.Alt) != 0;
    public bool HasShift => (Modifiers & ConsoleModifiers.Shift) != 0;
}

/// <summary>
/// Enumerates the focusable panels in the TUI.
/// </summary>
public enum FocusPanel
{
    Prompt,
    Activity,
    Context
}

/// <summary>
/// Actions that the key handler can produce.
/// </summary>
public enum KeyAction
{
    None,
    SubmitPrompt,
    InsertNewline,
    TogglePause,
    Cancel,
    CycleFocusForward,
    ViewResource1,
    ViewResource2,
    ViewResource3,
    ViewResource4,
    ViewResource5,
    ViewResource6,
    ViewResource7,
    ViewResource8,
    ViewResource9,
    ToggleExpand
}

/// <summary>
/// Central keyboard shortcut handler.
/// Routes key inputs to semantic actions based on current focus and state.
/// </summary>
public sealed class KeyboardHandler
{
    private static readonly FocusPanel[] FocusCycle = [FocusPanel.Prompt, FocusPanel.Activity, FocusPanel.Context];

    /// <summary>
    /// Maps a key input to a semantic action.
    /// </summary>
    /// <param name="input">The key event.</param>
    /// <param name="currentFocus">Which panel currently has focus.</param>
    /// <returns>The action to perform.</returns>
    public KeyAction Handle(KeyInput input, FocusPanel currentFocus)
    {
        // Ctrl+P: toggle pause/resume (works in any panel)
        if (input.HasCtrl && input.Key == ConsoleKey.P)
            return KeyAction.TogglePause;

        // Ctrl+C: cancel (works in any panel)
        if (input.HasCtrl && input.Key == ConsoleKey.C)
            return KeyAction.Cancel;

        // Tab: cycle focus
        if (input.Key == ConsoleKey.Tab && !input.HasCtrl && !input.HasAlt)
            return KeyAction.CycleFocusForward;

        // Enter: submit prompt (only when prompt focused)
        if (input.Key == ConsoleKey.Enter && !input.HasAlt && currentFocus == FocusPanel.Prompt)
            return KeyAction.SubmitPrompt;

        // Alt+Enter: insert newline (only when prompt focused)
        if (input.Key == ConsoleKey.Enter && input.HasAlt && currentFocus == FocusPanel.Prompt)
            return KeyAction.InsertNewline;

        // Number keys 1-9: view resource (only when prompt is NOT focused)
        if (currentFocus != FocusPanel.Prompt && !input.HasCtrl && !input.HasAlt)
        {
            var resourceAction = input.Key switch
            {
                ConsoleKey.D1 => KeyAction.ViewResource1,
                ConsoleKey.D2 => KeyAction.ViewResource2,
                ConsoleKey.D3 => KeyAction.ViewResource3,
                ConsoleKey.D4 => KeyAction.ViewResource4,
                ConsoleKey.D5 => KeyAction.ViewResource5,
                ConsoleKey.D6 => KeyAction.ViewResource6,
                ConsoleKey.D7 => KeyAction.ViewResource7,
                ConsoleKey.D8 => KeyAction.ViewResource8,
                ConsoleKey.D9 => KeyAction.ViewResource9,
                _ => KeyAction.None
            };
            if (resourceAction != KeyAction.None)
                return resourceAction;
        }

        // Space or Enter on activity/context: toggle expand
        if (currentFocus != FocusPanel.Prompt && input.Key is ConsoleKey.Spacebar or ConsoleKey.Enter)
            return KeyAction.ToggleExpand;

        return KeyAction.None;
    }

    /// <summary>
    /// Cycles to the next focus panel.
    /// </summary>
    public static FocusPanel CycleFocus(FocusPanel current)
    {
        var idx = Array.IndexOf(FocusCycle, current);
        return FocusCycle[(idx + 1) % FocusCycle.Length];
    }

    /// <summary>
    /// Gets context-aware keyboard hints for the current state.
    /// </summary>
    public static IReadOnlyList<string> GetHints(FocusPanel focus, bool isPaused)
    {
        var hints = new List<string>();

        if (focus == FocusPanel.Prompt)
        {
            hints.Add("Enter: Submit");
            hints.Add("Alt+Enter: Newline");
        }
        else
        {
            hints.Add("1-9: Resources");
            hints.Add("Space: Expand");
        }

        hints.Add("Tab: Focus");
        hints.Add(isPaused ? "Ctrl+P: Resume" : "Ctrl+P: Pause");
        hints.Add("Ctrl+C: Cancel");

        return hints;
    }
}
