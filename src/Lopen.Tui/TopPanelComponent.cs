using System.Text;

namespace Lopen.Tui;

/// <summary>
/// Renders the top panel containing logo, version, model, context usage,
/// premium requests, git branch, auth status, phase, and step progress.
/// </summary>
public sealed class TopPanelComponent : IPreviewableComponent
{
    /// <summary>ASCII art logo lines.</summary>
    internal static readonly string[] LogoLines =
    [
        "╻  ┏━┓┏━┓┏━╸┏┓╻",
        "┃  ┃ ┃┣━┛┣╸ ┃┗┫",
        "┗━╸┗━┛╹  ┗━╸╹ ╹",
    ];

    public string Name => "TopPanel";
    public string Description => "Top panel with logo, version, model, context, auth, phase/step";

    /// <summary>
    /// Renders the top panel as an array of plain-text lines sized to the given region width.
    /// </summary>
    public string[] Render(TopPanelData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var width = region.Width;
        var lines = new List<string>();

        var statusLine = BuildStatusLine(data);
        var phaseLine = BuildPhaseLine(data);

        if (data.ShowLogo && region.Height >= 3)
        {
            // Row 1: logo line 1 + status info
            lines.Add(CombineLogoAndContent(LogoLines[0], $"  {data.Version} │ {statusLine}", width));
            // Row 2: logo line 2
            lines.Add(PadToWidth(LogoLines[1], width));
            // Row 3: logo line 3 + phase/step
            lines.Add(CombineLogoAndContent(LogoLines[2], $"  {phaseLine}", width));
        }
        else
        {
            // No logo: compact header
            lines.Add(PadToWidth($"{data.Version} │ {statusLine}", width));
            if (region.Height >= 2 && !string.IsNullOrEmpty(phaseLine))
                lines.Add(PadToWidth(phaseLine, width));
        }

        // Pad remaining rows if region is taller
        while (lines.Count < region.Height)
            lines.Add(new string(' ', width));

        // Trim to region height
        return lines.GetRange(0, region.Height).ToArray();
    }

    /// <summary>
    /// Builds the status portion: model, context, premium, branch, auth.
    /// </summary>
    internal static string BuildStatusLine(TopPanelData data)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(data.ModelName))
            parts.Add(data.ModelName);

        parts.Add($"Context: {FormatTokens(data.ContextUsedTokens)}/{FormatTokens(data.ContextMaxTokens)}");

        if (data.PremiumRequestCount > 0)
            parts.Add($"🔥 {data.PremiumRequestCount} premium");

        if (!string.IsNullOrEmpty(data.GitBranch))
            parts.Add(data.GitBranch);

        parts.Add(data.IsAuthenticated ? "🟢" : "🔴");

        return string.Join(" │ ", parts);
    }

    /// <summary>
    /// Builds the phase/step line with progress indicator.
    /// </summary>
    internal static string BuildPhaseLine(TopPanelData data)
    {
        if (string.IsNullOrEmpty(data.PhaseName))
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append($"Phase: {data.PhaseName}");

        if (data.TotalSteps > 0)
        {
            sb.Append(' ');
            sb.Append(BuildStepIndicator(data.CurrentStep, data.TotalSteps));
            sb.Append($" Step {data.CurrentStep}/{data.TotalSteps}");

            if (!string.IsNullOrEmpty(data.StepLabel))
                sb.Append($": {data.StepLabel}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a step progress indicator like ●●●○○○○.
    /// </summary>
    internal static string BuildStepIndicator(int current, int total)
    {
        if (total <= 0)
            return string.Empty;

        var filled = Math.Clamp(current, 0, total);
        var sb = new StringBuilder(total);
        for (int i = 0; i < filled; i++)
            sb.Append('●');
        for (int i = filled; i < total; i++)
            sb.Append('○');
        return sb.ToString();
    }

    /// <summary>
    /// Formats a token count in human-readable form (e.g., 2400 → "2.4K", 128000 → "128K").
    /// </summary>
    internal static string FormatTokens(long tokens)
    {
        return tokens switch
        {
            < 1_000 => tokens.ToString(),
            < 1_000_000 => $"{tokens / 1000.0:0.#}K",
            _ => $"{tokens / 1_000_000.0:0.#}M",
        };
    }

    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated", "error", "loading"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new TopPanelData
            {
                Version = "v0.1.0",
            },
            "error" => new TopPanelData
            {
                Version = "v0.1.0",
                IsAuthenticated = false,
                ContextMaxTokens = 200000,
            },
            "loading" => new TopPanelData
            {
                Version = "v0.1.0",
                ModelName = "Loading...",
                ContextMaxTokens = 200000,
                IsAuthenticated = true,
                PhaseName = "Initializing",
            },
            _ => new TopPanelData
            {
                Version = "v0.1.0",
                ModelName = "claude-sonnet-4",
                ContextUsedTokens = 45000,
                ContextMaxTokens = 200000,
                PremiumRequestCount = 12,
                GitBranch = "feat/auth",
                IsAuthenticated = true,
                PhaseName = "Building",
                CurrentStep = 3,
                TotalSteps = 5,
                StepLabel = "Iterate Tasks",
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
    {
        var data = new TopPanelData
        {
            Version = "v0.1.0",
            ModelName = "claude-sonnet-4",
            ContextUsedTokens = 45000,
            ContextMaxTokens = 200000,
            PremiumRequestCount = 12,
            GitBranch = "feat/auth",
            IsAuthenticated = true,
            PhaseName = "Building",
            CurrentStep = 3,
            TotalSteps = 5,
            StepLabel = "Iterate Tasks",
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    private static string CombineLogoAndContent(string logo, string content, int width)
    {
        var combined = logo + content;
        return combined.Length >= width
            ? combined[..width]
            : combined.PadRight(width);
    }

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width
            ? text[..width]
            : text.PadRight(width);
    }
}
