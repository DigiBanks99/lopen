namespace Lopen.Tui;

/// <summary>
/// Immutable data bag consumed by <see cref="ContextPanelComponent"/> for rendering.
/// </summary>
public sealed record ContextPanelData
{
    /// <summary>Current task section data, or null if no task is active.</summary>
    public TaskSectionData? CurrentTask { get; init; }

    /// <summary>Component hierarchy data, or null if not available.</summary>
    public ComponentSectionData? Component { get; init; }

    /// <summary>Module hierarchy data, or null if not available.</summary>
    public ModuleSectionData? Module { get; init; }

    /// <summary>Active resources for numbered access.</summary>
    public IReadOnlyList<ResourceItem> Resources { get; init; } = [];
}

/// <summary>Current task with subtask tree.</summary>
public sealed record TaskSectionData
{
    /// <summary>Task name (e.g., "Implement JWT token validation").</summary>
    public required string Name { get; init; }

    /// <summary>Progress percentage (0–100).</summary>
    public int ProgressPercent { get; init; }

    /// <summary>Number of completed subtasks.</summary>
    public int CompletedSubtasks { get; init; }

    /// <summary>Total number of subtasks.</summary>
    public int TotalSubtasks { get; init; }

    /// <summary>Subtask list with status.</summary>
    public IReadOnlyList<SubtaskItem> Subtasks { get; init; } = [];
}

/// <summary>A subtask with its completion state.</summary>
public sealed record SubtaskItem(string Name, TaskState State);

/// <summary>Task state icons for the context panel tree.</summary>
public enum TaskState
{
    /// <summary>Not started (○).</summary>
    Pending,
    /// <summary>Currently being worked on (▶).</summary>
    InProgress,
    /// <summary>Successfully completed (✓).</summary>
    Complete,
    /// <summary>Failed and blocked (✗).</summary>
    Failed,
}

/// <summary>Component-level hierarchy data.</summary>
public sealed record ComponentSectionData
{
    /// <summary>Component name.</summary>
    public required string Name { get; init; }

    /// <summary>Number of completed tasks.</summary>
    public int CompletedTasks { get; init; }

    /// <summary>Total number of tasks.</summary>
    public int TotalTasks { get; init; }

    /// <summary>Task list with status.</summary>
    public IReadOnlyList<SubtaskItem> Tasks { get; init; } = [];
}

/// <summary>Module-level hierarchy data.</summary>
public sealed record ModuleSectionData
{
    /// <summary>Module name.</summary>
    public required string Name { get; init; }

    /// <summary>Number of components in progress.</summary>
    public int InProgressComponents { get; init; }

    /// <summary>Total number of components.</summary>
    public int TotalComponents { get; init; }

    /// <summary>Component list with status.</summary>
    public IReadOnlyList<SubtaskItem> Components { get; init; } = [];
}

/// <summary>An active resource document.</summary>
public sealed record ResourceItem(string Label, string? Content = null);
