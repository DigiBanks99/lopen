namespace Lopen.Tui;

/// <summary>
/// Renders the landing page modal shown on first startup.
/// Displays logo, version, quick commands, and auth status.
/// </summary>
public sealed class LandingPageComponent : ITuiComponent
{
    public string Name => "LandingPage";
    public string Description => "Landing page modal with quick commands and auth status";

    /// <summary>
    /// Renders the landing page as an array of plain-text lines.
    /// </summary>
    public string[] Render(LandingPageData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        var width = region.Width;

        // Logo (centered)
        foreach (var logoLine in TopPanelComponent.LogoLines)
            lines.Add(Center(logoLine, width));

        lines.Add(Center(data.Version, width));
        lines.Add(Center("Interactive Agent Loop", width));
        lines.Add(string.Empty);

        // Quick commands
        lines.Add("  Quick Commands");

        foreach (var cmd in data.QuickCommands)
        {
            lines.Add($"    {cmd.Command,-15}{cmd.Description}");
        }

        lines.Add(string.Empty);

        // Footer
        var authIcon = data.IsAuthenticated ? "🟢 Authenticated" : "🔴 Not authenticated";
        var footer = $"  Press any key to continue...{new string(' ', Math.Max(1, width - 30 - authIcon.Length - 2))}{authIcon}";
        lines.Add(footer);

        // Pad to region height
        while (lines.Count < region.Height)
            lines.Add(string.Empty);

        return lines.Take(region.Height).Select(l => PadToWidth(l, width)).ToArray();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width)
            return text[..width];
        var pad = (width - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width ? text[..width] : text.PadRight(width);
    }
}

/// <summary>
/// Renders the session resume modal when a previous active session is detected.
/// Shows session details and [Resume] [Start New] [View Details] options.
/// </summary>
public sealed class SessionResumeModalComponent : ITuiComponent
{
    /// <summary>Option labels for the resume modal.</summary>
    internal static readonly string[] Options = ["Resume", "Start New", "View Details"];

    public string Name => "SessionResumeModal";
    public string Description => "Session resume modal with Resume/Start New/View Details options";

    /// <summary>
    /// Renders the session resume modal as an array of plain-text lines.
    /// </summary>
    public string[] Render(SessionResumeData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        var width = region.Width;

        lines.Add($" Resume Session? {"─".PadRight(Math.Max(0, width - 20), '─')}╮");
        lines.Add(string.Empty);
        lines.Add($"  Previous session found for: {data.ModuleName} module");
        lines.Add($"  Phase: {data.PhaseName} (Step {data.StepProgress})");
        lines.Add($"  Progress: {data.ProgressPercent}% ({data.TaskProgress})");
        lines.Add($"  Last activity: {data.LastActivity}");
        lines.Add(string.Empty);

        // Options with selection highlight
        var optionParts = new List<string>();
        for (int i = 0; i < Options.Length; i++)
        {
            optionParts.Add(i == data.SelectedOption
                ? $"[>{Options[i]}<]"
                : $"[{Options[i]}]");
        }
        lines.Add($"  {string.Join("  ", optionParts)}");

        while (lines.Count < region.Height)
            lines.Add(string.Empty);

        return lines.Take(region.Height).Select(l => PadToWidth(l, width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width ? text[..width] : text.PadRight(width);
    }
}
