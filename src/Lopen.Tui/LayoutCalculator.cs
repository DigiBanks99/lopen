namespace Lopen.Tui;

/// <summary>
/// Calculates screen regions for the split-screen TUI layout.
/// Activity pane (left): 50-80% width. Context pane (right): 20-50% width.
/// </summary>
public static class LayoutCalculator
{
    /// <summary>Default header height in rows.</summary>
    public const int DefaultHeaderHeight = 4;

    /// <summary>Default prompt height in rows.</summary>
    public const int DefaultPromptHeight = 3;

    /// <summary>Minimum activity pane percentage (50%).</summary>
    public const int MinActivityPercent = 50;

    /// <summary>Maximum activity pane percentage (80%).</summary>
    public const int MaxActivityPercent = 80;

    /// <summary>
    /// Calculates layout regions from the total screen dimensions and split ratio.
    /// </summary>
    /// <param name="screenWidth">Total screen width in columns.</param>
    /// <param name="screenHeight">Total screen height in rows.</param>
    /// <param name="splitPercent">Activity pane width as a percentage (clamped to 50-80).</param>
    /// <param name="headerHeight">Header height in rows.</param>
    /// <param name="promptHeight">Prompt height in rows.</param>
    public static LayoutRegions Calculate(
        int screenWidth,
        int screenHeight,
        int splitPercent = 60,
        int headerHeight = DefaultHeaderHeight,
        int promptHeight = DefaultPromptHeight)
    {
        var clampedPercent = Math.Clamp(splitPercent, MinActivityPercent, MaxActivityPercent);
        var bodyHeight = Math.Max(0, screenHeight - headerHeight - promptHeight);

        var activityWidth = (int)(screenWidth * clampedPercent / 100.0);
        var contextWidth = screenWidth - activityWidth;

        return new LayoutRegions(
            Header: new ScreenRect(0, 0, screenWidth, headerHeight),
            Activity: new ScreenRect(0, headerHeight, activityWidth, bodyHeight),
            Context: new ScreenRect(activityWidth, headerHeight, contextWidth, bodyHeight),
            Prompt: new ScreenRect(0, headerHeight + bodyHeight, screenWidth, promptHeight));
    }
}
