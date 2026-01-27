using Spectre.Console.Rendering;

namespace Lopen.Core;

/// <summary>
/// Context for managing live layout updates during streaming operations.
/// Allows non-blocking updates to main content and side panel independently.
/// </summary>
public interface ILiveLayoutContext : IAsyncDisposable
{
    /// <summary>
    /// Updates the main content area without blocking.
    /// </summary>
    /// <param name="content">The new main content to display.</param>
    void UpdateMain(IRenderable content);

    /// <summary>
    /// Updates the side panel content without blocking.
    /// </summary>
    /// <param name="content">The new panel content to display.</param>
    void UpdatePanel(IRenderable content);

    /// <summary>
    /// Refreshes the display to show current updates.
    /// Call after making updates to reflect changes.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Gets whether the live context is currently active.
    /// </summary>
    bool IsActive { get; }
}

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

    /// <summary>
    /// Starts a live layout context for non-blocking updates during streaming.
    /// Use within a using statement to properly dispose the live context.
    /// </summary>
    /// <param name="initialMain">Initial main content to display.</param>
    /// <param name="initialPanel">Optional initial panel content.</param>
    /// <param name="config">Layout configuration (optional).</param>
    /// <param name="cancellationToken">Cancellation token to stop live updates.</param>
    /// <returns>A live layout context for making updates.</returns>
    Task<ILiveLayoutContext> StartLiveLayoutAsync(
        IRenderable initialMain,
        IRenderable? initialPanel = null,
        SplitLayoutConfig? config = null,
        CancellationToken cancellationToken = default);
}
