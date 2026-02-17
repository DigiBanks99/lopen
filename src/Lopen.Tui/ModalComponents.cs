namespace Lopen.Tui;

/// <summary>
/// Renders the landing page modal shown on first startup.
/// Displays logo, version, quick commands, and auth status.
/// </summary>
public sealed class LandingPageComponent : IPreviewableComponent
{
    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated", "error", "loading"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new LandingPageData
            {
                Version = "v0.1.0",
                IsAuthenticated = true,
                QuickCommands = [],
            },
            "error" => new LandingPageData
            {
                Version = "v0.1.0",
                IsAuthenticated = false,
                QuickCommands =
                [
                    new QuickCommand("build", "Build a module from spec"),
                    new QuickCommand("plan", "Generate a project plan"),
                    new QuickCommand("research", "Research a topic"),
                ],
            },
            "loading" => new LandingPageData
            {
                Version = "v0.1.0",
                IsAuthenticated = false,
                QuickCommands = [],
            },
            _ => new LandingPageData
            {
                Version = "v0.1.0",
                IsAuthenticated = true,
                QuickCommands =
                [
                    new QuickCommand("build", "Build a module from spec"),
                    new QuickCommand("plan", "Generate a project plan"),
                    new QuickCommand("research", "Research a topic"),
                ],
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new LandingPageData
        {
            Version = "v0.1.0",
            IsAuthenticated = true,
            QuickCommands =
            [
                new QuickCommand("build", "Build a module from spec"),
                new QuickCommand("plan", "Generate a project plan"),
                new QuickCommand("research", "Research a topic"),
            ],
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

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
public sealed class SessionResumeModalComponent : IPreviewableComponent
{
    /// <summary>Option labels for the resume modal.</summary>
    internal static readonly string[] Options = ["Resume", "Start New", "View Details"];

    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new SessionResumeData
            {
                ModuleName = "Unknown",
                PhaseName = "N/A",
                StepProgress = "0/0",
                TaskProgress = "0/0 tasks",
                LastActivity = "Unknown",
                ProgressPercent = 0,
                SelectedOption = 0,
            },
            _ => new SessionResumeData
            {
                ModuleName = "Authentication",
                PhaseName = "Building",
                StepProgress = "3/5",
                TaskProgress = "7/12 tasks",
                LastActivity = "2 hours ago",
                ProgressPercent = 58,
                SelectedOption = 0,
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new SessionResumeData
        {
            ModuleName = "Authentication",
            PhaseName = "Building",
            StepProgress = "3/5",
            TaskProgress = "7/12 tasks",
            LastActivity = "2 hours ago",
            ProgressPercent = 58,
            SelectedOption = 0,
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

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

/// <summary>
/// Renders the resource viewer modal showing scrollable content of an active resource.
/// Displays title bar, content lines with scroll, and footer with Esc/arrow hints.
/// </summary>
public sealed class ResourceViewerModalComponent : IPreviewableComponent
{
    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new ResourceViewerData
            {
                Label = "EMPTY.md",
                Lines = [],
            },
            _ => new ResourceViewerData
            {
                Label = "SPECIFICATION.md",
                Lines =
                [
                    "# Authentication Module",
                    "",
                    "## Overview",
                    "This module handles JWT-based authentication.",
                    "",
                    "## Requirements",
                    "- Token validation with RS256",
                    "- Refresh token rotation",
                    "- Role-based access control",
                ],
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new ResourceViewerData
        {
            Label = "SPECIFICATION.md",
            Lines =
            [
                "# Authentication Module",
                "",
                "## Overview",
                "This module handles JWT-based authentication.",
                "",
                "## Requirements",
                "- Token validation with RS256",
                "- Refresh token rotation",
                "- Role-based access control",
            ],
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string Name => "ResourceViewer";
    public string Description => "Resource viewer modal with scrollable content";

    /// <summary>
    /// Renders the resource viewer as an array of plain-text lines.
    /// </summary>
    public string[] Render(ResourceViewerData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        var width = region.Width;
        // Reserve 3 lines: title, separator, footer
        var contentHeight = Math.Max(0, region.Height - 3);

        // Title bar
        var title = $" 📄 {data.Label} ";
        var bar = title.Length < width
            ? title + new string('─', width - title.Length)
            : title[..width];
        lines.Add(bar);

        // Content lines with scroll
        var offset = Math.Clamp(data.ScrollOffset, 0, Math.Max(0, data.Lines.Count - contentHeight));
        var visible = data.Lines.Skip(offset).Take(contentHeight);
        foreach (var line in visible)
        {
            lines.Add(line.Length >= width ? line[..width] : line);
        }

        // Pad if content doesn't fill region
        while (lines.Count < region.Height - 1)
            lines.Add(string.Empty);

        // Footer
        var scrollInfo = data.Lines.Count > contentHeight
            ? $"Line {offset + 1}-{Math.Min(offset + contentHeight, data.Lines.Count)} of {data.Lines.Count}"
            : "All content visible";
        lines.Add($" Esc: Close  ↑/↓: Scroll  {scrollInfo}");

        return lines.Take(region.Height).Select(l => PadToWidth(l, width)).ToArray();
    }

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width ? text[..width] : text.PadRight(width);
    }
}
