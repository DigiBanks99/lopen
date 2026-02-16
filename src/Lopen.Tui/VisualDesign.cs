namespace Lopen.Tui;

/// <summary>
/// Semantic color palette for the TUI with NO_COLOR support.
/// Provides named colors that components reference instead of hardcoded values.
/// </summary>
public sealed class ColorPalette
{
    /// <summary>Whether colors are disabled (NO_COLOR env var set).</summary>
    public bool NoColor { get; }

    public ColorPalette(bool noColor = false)
    {
        NoColor = noColor || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    // Semantic color names
    public string Success => NoColor ? "" : "\x1b[32m";
    public string Error => NoColor ? "" : "\x1b[31m";
    public string Warning => NoColor ? "" : "\x1b[33m";
    public string Info => NoColor ? "" : "\x1b[36m";
    public string Muted => NoColor ? "" : "\x1b[90m";
    public string Accent => NoColor ? "" : "\x1b[35m";
    public string Reset => NoColor ? "" : "\x1b[0m";
    public string Bold => NoColor ? "" : "\x1b[1m";
}

/// <summary>
/// Unicode/ASCII fallback support for box drawing and icons.
/// </summary>
public static class UnicodeSupport
{
    /// <summary>Whether to use ASCII fallbacks instead of Unicode.</summary>
    public static bool UseAscii { get; set; }

    // Box drawing
    public static string TopLeft => UseAscii ? "+" : "┌";
    public static string TopRight => UseAscii ? "+" : "┐";
    public static string BottomLeft => UseAscii ? "+" : "└";
    public static string BottomRight => UseAscii ? "+" : "┘";
    public static string Horizontal => UseAscii ? "-" : "─";
    public static string Vertical => UseAscii ? "|" : "│";
    public static string TeeRight => UseAscii ? "+" : "├";
    public static string TeeLeft => UseAscii ? "+" : "┤";
    public static string Cross => UseAscii ? "+" : "┼";

    // Status icons
    public static string CheckMark => UseAscii ? "[x]" : "✓";
    public static string Cross_Icon => UseAscii ? "[!]" : "✗";
    public static string Arrow => UseAscii ? ">" : "▶";
    public static string Circle => UseAscii ? "o" : "○";
    public static string FilledCircle => UseAscii ? "*" : "●";
    public static string Diamond => UseAscii ? "<>" : "◆";
    public static string Warning_Icon => UseAscii ? "!!" : "⚠";
}
