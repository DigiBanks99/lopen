using Spectre.Console.Rendering;

namespace Lopen.Core;

/// <summary>
/// Status of a task in a task list panel.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task not started.</summary>
    Pending,

    /// <summary>Task currently in progress.</summary>
    InProgress,

    /// <summary>Task completed successfully.</summary>
    Completed,

    /// <summary>Task failed.</summary>
    Failed
}

/// <summary>
/// Represents a task item for display in a task panel.
/// </summary>
public record TaskItem
{
    /// <summary>Task name/description.</summary>
    public required string Name { get; init; }

    /// <summary>Current status of the task.</summary>
    public TaskStatus Status { get; init; } = TaskStatus.Pending;
}

/// <summary>
/// Configuration for split layout rendering.
/// </summary>
public record SplitLayoutConfig
{
    /// <summary>Minimum terminal width to enable split layout. Default is 100.</summary>
    public int MinWidthForSplit { get; init; } = 100;

    /// <summary>Ratio for main content area (out of 10). Default is 7 (70%).</summary>
    public int MainRatio { get; init; } = 7;

    /// <summary>Ratio for side panel area (out of 10). Default is 3 (30%).</summary>
    public int PanelRatio { get; init; } = 3;
}

/// <summary>
/// Renderer for split-screen layouts with right-side panels.
/// </summary>
public interface ILayoutRenderer
{
    /// <summary>
    /// Render a split layout with main content and an optional side panel.
    /// Falls back to full-width main content if terminal is too narrow.
    /// </summary>
    /// <param name="mainContent">The main content to display.</param>
    /// <param name="sidePanel">Optional side panel content.</param>
    /// <param name="config">Layout configuration (optional).</param>
    void RenderSplitLayout(
        IRenderable mainContent,
        IRenderable? sidePanel = null,
        SplitLayoutConfig? config = null);

    /// <summary>
    /// Render a task list panel with status indicators.
    /// </summary>
    /// <param name="tasks">List of tasks to display.</param>
    /// <param name="title">Panel title.</param>
    /// <returns>A renderable panel containing the task list.</returns>
    IRenderable RenderTaskPanel(IReadOnlyList<TaskItem> tasks, string title = "Progress");

    /// <summary>
    /// Render a context panel with key-value metadata.
    /// </summary>
    /// <param name="data">Key-value pairs to display.</param>
    /// <param name="title">Panel title.</param>
    /// <returns>A renderable panel containing the metadata.</returns>
    IRenderable RenderContextPanel(IReadOnlyDictionary<string, string> data, string title = "Context");

    /// <summary>
    /// Gets the current terminal width.
    /// </summary>
    int TerminalWidth { get; }
}
