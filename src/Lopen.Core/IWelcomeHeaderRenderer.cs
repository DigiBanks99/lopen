namespace Lopen.Core;

/// <summary>
/// Renderer for the welcome header displayed at REPL startup.
/// </summary>
public interface IWelcomeHeaderRenderer
{
    /// <summary>
    /// Renders the welcome header to the console.
    /// </summary>
    /// <param name="context">The context containing header data.</param>
    void RenderWelcomeHeader(WelcomeHeaderContext context);
}

/// <summary>
/// Context data for rendering the welcome header.
/// </summary>
public record WelcomeHeaderContext
{
    /// <summary>Application version string (e.g., "1.0.0-alpha").</summary>
    public string Version { get; init; } = "";

    /// <summary>Session name (auto-generated or user-specified).</summary>
    public string SessionName { get; init; } = "";

    /// <summary>Context window capacity information.</summary>
    public ContextWindowInfo ContextWindow { get; init; } = new();

    /// <summary>Display preferences.</summary>
    public WelcomeHeaderPreferences Preferences { get; init; } = new();

    /// <summary>Terminal capabilities for responsive layout.</summary>
    public ITerminalCapabilities? Terminal { get; init; }
}

/// <summary>
/// Context window capacity information for token/message tracking.
/// </summary>
public record ContextWindowInfo
{
    /// <summary>Tokens used in current session (null if unavailable).</summary>
    public long? TokensUsed { get; init; }

    /// <summary>Total token capacity (null if unavailable).</summary>
    public long? TokensTotal { get; init; }

    /// <summary>Number of messages in the conversation.</summary>
    public int MessageCount { get; init; }

    /// <summary>Whether token information is available.</summary>
    public bool HasTokenInfo => TokensUsed.HasValue && TokensTotal.HasValue;

    /// <summary>Context usage as a percentage (0-100).</summary>
    public double UsagePercent => HasTokenInfo
        ? (double)TokensUsed!.Value / TokensTotal!.Value * 100
        : 0;

    /// <summary>Format context info for display.</summary>
    public string GetDisplayText()
    {
        if (HasTokenInfo)
        {
            var used = FormatTokenCount(TokensUsed!.Value);
            var total = FormatTokenCount(TokensTotal!.Value);
            return $"{used}/{total} tokens";
        }
        return MessageCount == 1 ? "1 message" : $"{MessageCount} messages";
    }

    private static string FormatTokenCount(long count) => count switch
    {
        >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
        >= 1_000 => $"{count / 1_000.0:F1}K",
        _ => count.ToString()
    };
}

/// <summary>
/// Preferences for welcome header display.
/// </summary>
public record WelcomeHeaderPreferences
{
    /// <summary>Whether to show the ASCII logo.</summary>
    public bool ShowLogo { get; init; } = true;

    /// <summary>Whether to show the help tip.</summary>
    public bool ShowTip { get; init; } = true;

    /// <summary>Whether to show context window info.</summary>
    public bool ShowContext { get; init; } = true;

    /// <summary>Whether to show session info.</summary>
    public bool ShowSession { get; init; } = true;
}
