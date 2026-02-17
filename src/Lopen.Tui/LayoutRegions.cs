namespace Lopen.Tui;

/// <summary>
/// Holds the calculated screen regions for the main TUI layout.
/// </summary>
/// <param name="Header">Top panel area (logo, version, model, context usage, auth status).</param>
/// <param name="Activity">Left pane — scrollable agent activity feed.</param>
/// <param name="Context">Right pane — persistent work context.</param>
/// <param name="Prompt">Bottom area — multi-line text input.</param>
public readonly record struct LayoutRegions(
    ScreenRect Header,
    ScreenRect Activity,
    ScreenRect Context,
    ScreenRect Prompt);

/// <summary>
/// Simple rectangle value for layout calculations (decoupled from Spectre.Tui.Rectangle).
/// </summary>
/// <param name="X">Left column.</param>
/// <param name="Y">Top row.</param>
/// <param name="Width">Width in columns.</param>
/// <param name="Height">Height in rows.</param>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    /// <summary>Shrinks the rectangle by the specified amounts on all sides.</summary>
    public ScreenRect Inflate(int horizontal, int vertical) =>
        new(X - horizontal, Y - vertical, Width + 2 * horizontal, Height + 2 * vertical);
}
