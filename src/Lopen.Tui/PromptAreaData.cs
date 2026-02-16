namespace Lopen.Tui;

/// <summary>
/// Immutable data bag consumed by <see cref="PromptAreaComponent"/> for rendering.
/// </summary>
public sealed record PromptAreaData
{
    /// <summary>Current text in the prompt input.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Cursor position within the text (character offset).</summary>
    public int CursorPosition { get; init; }

    /// <summary>Placeholder text shown when input is empty.</summary>
    public string Placeholder { get; init; } = "Your prompt here (or let Lopen continue working)...";

    /// <summary>Whether the agent is currently paused.</summary>
    public bool IsPaused { get; init; }

    /// <summary>Custom keyboard hints. If null, uses default hints.</summary>
    public IReadOnlyList<string>? CustomHints { get; init; }

    /// <summary>Spinner data for async operation feedback. Null when no operation is running.</summary>
    public SpinnerData? Spinner { get; init; }
}
