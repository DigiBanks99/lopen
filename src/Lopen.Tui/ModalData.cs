namespace Lopen.Tui;

/// <summary>
/// Immutable data bag consumed by <see cref="LandingPageComponent"/> for rendering.
/// </summary>
public sealed record LandingPageData
{
    /// <summary>Application version string.</summary>
    public required string Version { get; init; }

    /// <summary>Whether the user is authenticated.</summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>Quick commands to display.</summary>
    public IReadOnlyList<QuickCommand> QuickCommands { get; init; } = DefaultQuickCommands;

    /// <summary>Default quick commands shown on the landing page.</summary>
    public static readonly QuickCommand[] DefaultQuickCommands =
    [
        new("/help", "Show available commands"),
        new("/spec", "Start requirement gathering"),
        new("/plan", "Start planning mode"),
        new("/build", "Start build mode"),
        new("/session", "Manage sessions"),
    ];
}

/// <summary>A quick command entry for the landing page.</summary>
public sealed record QuickCommand(string Command, string Description);

/// <summary>
/// Immutable data bag consumed by <see cref="SessionResumeModalComponent"/> for rendering.
/// </summary>
public sealed record SessionResumeData
{
    /// <summary>Module name of the session.</summary>
    public required string ModuleName { get; init; }

    /// <summary>Current phase name.</summary>
    public required string PhaseName { get; init; }

    /// <summary>Current step / total steps (e.g., "6/7").</summary>
    public required string StepProgress { get; init; }

    /// <summary>Overall progress percentage.</summary>
    public int ProgressPercent { get; init; }

    /// <summary>Completed tasks / total tasks description.</summary>
    public required string TaskProgress { get; init; }

    /// <summary>Relative time since last activity (e.g., "2 hours ago").</summary>
    public required string LastActivity { get; init; }

    /// <summary>Currently selected option index (0=Resume, 1=Start New, 2=View Details).</summary>
    public int SelectedOption { get; init; }
}
