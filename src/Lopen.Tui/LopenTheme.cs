using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Centralized theme for the Lopen TUI using terminal-native ANSI 0-15 colours.
/// All UI components reference colours by semantic role, never by direct colour value.
/// </summary>
public static class LopenTheme
{
    // ── Semantic Colour Roles (ANSI 0-15) ──────────────────────────────

    /// <summary>Prompt char, active elements, primary text accents. ANSI Blue (12).</summary>
    public static readonly Color Primary = Color.Blue;

    /// <summary>Phase labels, secondary accents. ANSI Purple (5).</summary>
    public static readonly Color Secondary = Color.Purple;

    /// <summary>Links, highlights, interactive elements. ANSI Aqua (14).</summary>
    public static readonly Color Accent = Color.Aqua;

    /// <summary>Completed phases, successful tool calls. ANSI Lime (10).</summary>
    public static readonly Color Success = Color.Lime;

    /// <summary>Warnings, budget approaching limit. ANSI Yellow (11).</summary>
    public static readonly Color Warning = Color.Yellow;

    /// <summary>Errors, failed tool calls, critical states. ANSI Red (9).</summary>
    public static readonly Color Error = Color.Red;

    /// <summary>Spinners, secondary information, timestamps. ANSI Grey (8).</summary>
    public static readonly Color Muted = Color.Grey;

    /// <summary>Primary text content. ANSI White (15).</summary>
    public static readonly Color Text = Color.White;

    // ── Styles ─────────────────────────────────────────────────────────

    /// <summary>Bold primary style for active elements.</summary>
    public static readonly Style PrimaryBold = new(Primary, decoration: Decoration.Bold);

    /// <summary>Dim muted style for secondary information.</summary>
    public static readonly Style MutedDim = new(Muted, decoration: Decoration.Dim);

    /// <summary>Bold error style for failures and critical states.</summary>
    public static readonly Style ErrorBold = new(Error, decoration: Decoration.Bold);

    /// <summary>Accent style for interactive elements.</summary>
    public static readonly Style AccentStyle = new(Accent);

    /// <summary>Success style for completed items.</summary>
    public static readonly Style SuccessStyle = new(Success);

    /// <summary>Warning style for caution states.</summary>
    public static readonly Style WarningStyle = new(Warning);

    /// <summary>Secondary style for phase labels.</summary>
    public static readonly Style SecondaryStyle = new(Secondary);

    /// <summary>Plain text style.</summary>
    public static readonly Style TextStyle = new(Text);

    // ── Unicode Glyphs ─────────────────────────────────────────────────

    /// <summary>Prompt character: ❯ (U+276F)</summary>
    public const string PromptChar = "\u276F";

    /// <summary>Section marker: ◆ (U+25C6)</summary>
    public const string SectionMarker = "\u25C6";

    /// <summary>Phase complete indicator: ✓ (U+2713)</summary>
    public const string PhaseComplete = "\u2713";

    /// <summary>Phase active indicator: ● (U+25CF)</summary>
    public const string PhaseActive = "\u25CF";

    /// <summary>Phase pending indicator: ○ (U+25CB)</summary>
    public const string PhasePending = "\u25CB";

    /// <summary>Bullet list item: • (U+2022)</summary>
    public const string Bullet = "\u2022";

    /// <summary>Successful tool call: ✔ (U+2714)</summary>
    public const string ToolSuccess = "\u2714";

    /// <summary>Failed tool call: ✘ (U+2718)</summary>
    public const string ToolFailure = "\u2718";

    /// <summary>Pause indicator: ⏸ (U+23F8)</summary>
    public const string PauseIndicator = "\u23F8";

    /// <summary>Informational hint: ℹ (U+2139)</summary>
    public const string InfoHint = "\u2139";

    // ── Markup Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Returns a Spectre markup string with the text escaped and styled with the given colour.
    /// </summary>
    public static string Styled(string text, Color color)
    {
        return $"[{color.ToMarkup()}]{Markup.Escape(text)}[/]";
    }

    /// <summary>
    /// Returns a Spectre markup string with the text escaped and styled bold with the given colour.
    /// </summary>
    public static string Bold(string text, Color color)
    {
        return $"[bold {color.ToMarkup()}]{Markup.Escape(text)}[/]";
    }

    /// <summary>
    /// Returns a Spectre markup string with the text escaped and styled dim with the given colour.
    /// </summary>
    public static string Dim(string text, Color color)
    {
        return $"[dim {color.ToMarkup()}]{Markup.Escape(text)}[/]";
    }
}
