namespace Lopen.Tui;

/// <summary>
/// Renders the main activity area (left pane) with scrolling and progressive disclosure.
/// Current action expanded, previous actions collapsed to summaries.
/// </summary>
public sealed class ActivityPanelComponent : IPreviewableComponent
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

        var palette = new ColorPalette();

        // Build all visible lines from entries
        var allLines = new List<string>();

        for (int i = 0; i < data.Entries.Count; i++)
        {
            var entry = data.Entries[i];
            var prefix = KindPrefix(entry.Kind);
            var expandIndicator = entry.HasDetails
                ? (entry.IsExpanded ? "▼" : "▶")
                : " ";
            var selectionMarker = i == data.SelectedEntryIndex ? ">" : " ";

            // Color error entries
            var summary = entry.Kind == ActivityEntryKind.Error
                ? $"{palette.Error}{entry.Summary}{palette.Reset}"
                : entry.Summary;

            allLines.Add($"{selectionMarker}{expandIndicator}{prefix} {summary}");

            if (entry.IsExpanded && entry.Details.Count > 0)
            {
                foreach (var detail in entry.Details)
                    allLines.Add($"  {HighlightDetailLine(detail, palette)}");

                if (entry.FullDocumentContent is not null)
                    allLines.Add("  [Press Enter to view full document]");
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

    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated", "error", "loading"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new ActivityPanelData(),
            "error" => new ActivityPanelData
            {
                Entries =
                [
                    new ActivityEntry { Summary = "Reading project structure", Kind = ActivityEntryKind.Action },
                    new ActivityEntry { Summary = "Build failed: missing reference", Kind = ActivityEntryKind.Error },
                    new ActivityEntry { Summary = "error CS1061: 'AuthToken' does not contain 'Expiry'", Kind = ActivityEntryKind.Error },
                ],
            },
            "loading" => new ActivityPanelData
            {
                Entries =
                [
                    new ActivityEntry { Summary = "Processing...", Kind = ActivityEntryKind.Action },
                ],
            },
            _ => new ActivityPanelData
            {
                Entries =
                [
                    new ActivityEntry { Summary = "Reading project structure", Kind = ActivityEntryKind.Action },
                    new ActivityEntry
                    {
                        Summary = "Edited src/Auth/JwtValidator.cs",
                        Kind = ActivityEntryKind.FileEdit,
                        IsExpanded = true,
                        Details = ["+ Added token expiry check", "+ Added signing key validation"],
                    },
                    new ActivityEntry { Summary = "dotnet build src/Auth/", Kind = ActivityEntryKind.Command },
                    new ActivityEntry { Summary = "Build failed: missing reference", Kind = ActivityEntryKind.Error },
                    new ActivityEntry { Summary = "Researching JWT best practices", Kind = ActivityEntryKind.Research },
                ],
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry { Summary = "Reading project structure", Kind = ActivityEntryKind.Action },
                new ActivityEntry
                {
                    Summary = "Edited src/Auth/JwtValidator.cs",
                    Kind = ActivityEntryKind.FileEdit,
                    IsExpanded = true,
                    Details = ["+ Added token expiry check", "+ Added signing key validation"],
                },
                new ActivityEntry { Summary = "dotnet build src/Auth/", Kind = ActivityEntryKind.Command },
                new ActivityEntry { Summary = "Build failed: missing reference", Kind = ActivityEntryKind.Error },
                new ActivityEntry { Summary = "Researching JWT best practices", Kind = ActivityEntryKind.Research },
            ],
        };
        return Render(data, new ScreenRect(0, 0, width, height));
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
        ActivityEntryKind.ToolCall => "⚙",
        ActivityEntryKind.Research => "📖",
        ActivityEntryKind.Conversation => "💬",
        _ => "●",
    };

    internal static string HighlightDetailLine(string detail, ColorPalette palette)
    {
        if (string.IsNullOrEmpty(detail)) return detail;
        return detail[0] switch
        {
            '+' => $"{palette.Success}{detail}{palette.Reset}",
            '-' => $"{palette.Error}{detail}{palette.Reset}",
            _ => detail,
        };
    }

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width
            ? text[..width]
            : text.PadRight(width);
    }
}
