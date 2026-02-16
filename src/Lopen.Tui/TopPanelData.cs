namespace Lopen.Tui;

/// <summary>
/// Immutable data bag consumed by <see cref="TopPanelComponent"/> for rendering.
/// </summary>
public sealed record TopPanelData
{
    /// <summary>Application version string (e.g., "v1.0.0").</summary>
    public required string Version { get; init; }

    /// <summary>Active AI model name (e.g., "claude-opus-4.6").</summary>
    public string? ModelName { get; init; }

    /// <summary>Tokens consumed in the current context window.</summary>
    public long ContextUsedTokens { get; init; }

    /// <summary>Maximum context window size in tokens.</summary>
    public long ContextMaxTokens { get; init; }

    /// <summary>Number of premium API requests consumed.</summary>
    public int PremiumRequestCount { get; init; }

    /// <summary>Current git branch name, or null if not in a repository.</summary>
    public string? GitBranch { get; init; }

    /// <summary>Whether the user is currently authenticated.</summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>Current workflow phase name (Requirement Gathering / Planning / Building).</summary>
    public string? PhaseName { get; init; }

    /// <summary>Current step number (1-based).</summary>
    public int CurrentStep { get; init; }

    /// <summary>Total number of steps in the workflow.</summary>
    public int TotalSteps { get; init; }

    /// <summary>Label for the current step (e.g., "Iterate Tasks").</summary>
    public string? StepLabel { get; init; }

    /// <summary>Whether to show the ASCII logo. Controlled by --no-logo flag.</summary>
    public bool ShowLogo { get; init; } = true;
}
