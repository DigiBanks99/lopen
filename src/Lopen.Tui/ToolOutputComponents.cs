namespace Lopen.Tui;

/// <summary>
/// Renders diff viewer, phase transition summaries, and research display components.
/// </summary>
public sealed class DiffViewerComponent : ITuiComponent
{
    public string Name => "DiffViewer";
    public string Description => "Diff viewer with line numbers and add/remove markers";

    /// <summary>
    /// Renders a diff view as an array of plain-text lines.
    /// </summary>
    public string[] Render(DiffViewerData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();

        // Header: file path + stats
        lines.Add($"  {data.FilePath} (+{data.LinesAdded} -{data.LinesRemoved})");

        foreach (var hunk in data.Hunks)
        {
            int lineNum = hunk.StartLine;
            foreach (var line in hunk.Lines)
            {
                var prefix = line.Length > 0 ? line[0] : ' ';
                var numStr = prefix switch
                {
                    '+' => "   ",
                    '-' => $"{lineNum,3}",
                    _ => $"{lineNum,3}",
                };

                lines.Add($"  {numStr} │ {line}");

                if (prefix != '+')
                    lineNum++;
            }
        }

        // Pad to region
        while (lines.Count < region.Height)
            lines.Add(string.Empty);

        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}

/// <summary>
/// Renders phase transition summaries with collapsible sections.
/// </summary>
public sealed class PhaseTransitionComponent : ITuiComponent
{
    public string Name => "PhaseTransition";
    public string Description => "Phase transition summary with collapsible sections";

    /// <summary>
    /// Renders a phase transition summary as an array of plain-text lines.
    /// </summary>
    public string[] Render(PhaseTransitionData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();

        lines.Add($"◆ Phase Transition: {data.FromPhase} → {data.ToPhase}");

        foreach (var section in data.Sections)
        {
            lines.Add($"  ▸ {section.Title}");
            foreach (var item in section.Items)
            {
                lines.Add($"    • {item}");
            }
        }

        while (lines.Count < region.Height)
            lines.Add(string.Empty);

        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}

/// <summary>
/// Renders inline research display with findings.
/// </summary>
public sealed class ResearchDisplayComponent : ITuiComponent
{
    public string Name => "ResearchDisplay";
    public string Description => "Inline research display with findings and document link";

    /// <summary>
    /// Renders research findings as an array of plain-text lines.
    /// </summary>
    public string[] Render(ResearchDisplayData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();

        lines.Add($"📖 Research: {data.Topic}");

        foreach (var finding in data.Findings)
        {
            lines.Add($"  Finding: {finding}");
        }

        if (data.HasFullDocument)
        {
            lines.Add("  [See full research document]");
        }

        while (lines.Count < region.Height)
            lines.Add(string.Empty);

        return lines.Take(region.Height).Select(l => PadToWidth(l, region.Width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}
