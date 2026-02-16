namespace Lopen.Tui;

/// <summary>
/// Renders the main activity area (left pane) with scrolling and progressive disclosure.
/// Current action expanded, previous actions collapsed to summaries.
/// </summary>
public sealed class ActivityPanelComponent : ITuiComponent
{
    public string Name => "ActivityPanel";
    public string Description => "Main activity area with scrolling and progressive disclosure";

    /// <summary>
    /// Renders the activity panel as an array of plain-text lines sized to the given region.
    /// </summary>
    public string[] Render(ActivityPanelData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        // Build all visible lines from entries
        var allLines = new List<string>();

        for (int i = 0; i < data.Entries.Count; i++)
        {
            var entry = data.Entries[i];
            var prefix = KindPrefix(entry.Kind);

            allLines.Add($"{prefix} {entry.Summary}");

            if (entry.IsExpanded && entry.Details.Count > 0)
            {
                foreach (var detail in entry.Details)
                    allLines.Add($"  {detail}");
            }
        }

        if (allLines.Count == 0)
            allLines.Add(string.Empty);

        // Apply scroll offset
        int scrollOffset = data.ScrollOffset;
        if (scrollOffset < 0)
        {
            // Auto-scroll: show the last `region.Height` lines
            scrollOffset = Math.Max(0, allLines.Count - region.Height);
        }

        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, allLines.Count - 1));

        // Extract visible window
        var visibleLines = allLines
            .Skip(scrollOffset)
            .Take(region.Height)
            .ToList();

        // Pad remaining rows
        while (visibleLines.Count < region.Height)
            visibleLines.Add(string.Empty);

        // Pad/truncate each line to region width
        return visibleLines
            .Select(l => PadToWidth(l, region.Width))
            .ToArray();
    }

    /// <summary>
    /// Calculates the total number of rendered lines for all entries.
    /// Useful for determining scroll range.
    /// </summary>
    public static int CalculateTotalLines(ActivityPanelData data)
    {
        int total = 0;
        foreach (var entry in data.Entries)
        {
            total++; // summary line
            if (entry.IsExpanded)
                total += entry.Details.Count;
        }
        return total;
    }

    /// <summary>
    /// Returns the prefix character for an entry kind.
    /// </summary>
    internal static string KindPrefix(ActivityEntryKind kind) => kind switch
    {
        ActivityEntryKind.Action => "●",
        ActivityEntryKind.FileEdit => "●",
        ActivityEntryKind.Command => "$",
        ActivityEntryKind.TestResult => "✓",
        ActivityEntryKind.PhaseTransition => "◆",
        ActivityEntryKind.Error => "⚠",
        _ => "●",
    };

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width
            ? text[..width]
            : text.PadRight(width);
    }
}
