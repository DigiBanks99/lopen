namespace Lopen.Tui;

/// <summary>
/// Immutable data bag consumed by <see cref="ActivityPanelComponent"/> for rendering.
/// </summary>
public sealed record ActivityPanelData
{
    /// <summary>Ordered list of activity entries (newest last).</summary>
    public IReadOnlyList<ActivityEntry> Entries { get; init; } = [];

    /// <summary>Index of the scroll position (0 = top). -1 means auto-scroll to bottom.</summary>
    public int ScrollOffset { get; init; } = -1;

    /// <summary>Index of the currently selected entry for keyboard navigation. -1 means no selection.</summary>
    public int SelectedEntryIndex { get; init; } = -1;
}

/// <summary>
/// A single entry in the activity area. Can be expanded or collapsed.
/// </summary>
public sealed record ActivityEntry
{
    /// <summary>Summary line shown when collapsed (e.g., "● Edit src/auth.ts (+45 -12)").</summary>
    public required string Summary { get; init; }

    /// <summary>Detailed content shown when expanded.</summary>
    public IReadOnlyList<string> Details { get; init; } = [];

    /// <summary>Whether this entry is expanded.</summary>
    public bool IsExpanded { get; init; }

    /// <summary>Entry type for visual styling.</summary>
    public ActivityEntryKind Kind { get; init; } = ActivityEntryKind.Action;

    /// <summary>Optional full document content for drill-into (e.g., research documents).</summary>
    public string? FullDocumentContent { get; init; }

    /// <summary>Whether this entry has expandable details.</summary>
    public bool HasDetails => Details.Count > 0;
}

/// <summary>
/// Types of activity entries for rendering hints.
/// </summary>
public enum ActivityEntryKind
{
    /// <summary>Agent narrative or action.</summary>
    Action,
    /// <summary>File edit with diff.</summary>
    FileEdit,
    /// <summary>Command execution.</summary>
    Command,
    /// <summary>Test results.</summary>
    TestResult,
    /// <summary>Phase transition summary.</summary>
    PhaseTransition,
    /// <summary>Error or warning (auto-expands).</summary>
    Error,
    /// <summary>Tool call (e.g., code editing, file operations).</summary>
    ToolCall,
    /// <summary>Research finding with optional drill-into document.</summary>
    Research,
}
