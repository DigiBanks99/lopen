using Spectre.Console;
using Spectre.Console.Rendering;

namespace Lopen.Core;

/// <summary>
/// Mock layout renderer for testing. Records all render calls for verification.
/// </summary>
public class MockLayoutRenderer : ILayoutRenderer
{
    private readonly List<SplitLayoutCall> _splitLayoutCalls = new();
    private readonly List<TaskPanelCall> _taskPanelCalls = new();
    private readonly List<ContextPanelCall> _contextPanelCalls = new();

    /// <summary>
    /// Gets or sets the simulated terminal width. Default is 120.
    /// </summary>
    public int SimulatedWidth { get; set; } = 120;

    /// <inheritdoc />
    public int TerminalWidth => SimulatedWidth;

    /// <summary>
    /// Gets all split layout render calls.
    /// </summary>
    public IReadOnlyList<SplitLayoutCall> SplitLayoutCalls => _splitLayoutCalls.AsReadOnly();

    /// <summary>
    /// Gets all task panel render calls.
    /// </summary>
    public IReadOnlyList<TaskPanelCall> TaskPanelCalls => _taskPanelCalls.AsReadOnly();

    /// <summary>
    /// Gets all context panel render calls.
    /// </summary>
    public IReadOnlyList<ContextPanelCall> ContextPanelCalls => _contextPanelCalls.AsReadOnly();

    /// <inheritdoc />
    public void RenderSplitLayout(
        IRenderable mainContent,
        IRenderable? sidePanel = null,
        SplitLayoutConfig? config = null)
    {
        config ??= new SplitLayoutConfig();

        _splitLayoutCalls.Add(new SplitLayoutCall
        {
            MainContent = mainContent,
            SidePanel = sidePanel,
            Config = config,
            TerminalWidth = SimulatedWidth,
            WasSplit = SimulatedWidth >= config.MinWidthForSplit && sidePanel != null
        });
    }

    /// <inheritdoc />
    public IRenderable RenderTaskPanel(IReadOnlyList<TaskItem> tasks, string title = "Progress")
    {
        _taskPanelCalls.Add(new TaskPanelCall
        {
            Tasks = tasks.ToList(),
            Title = title
        });

        // Return a simple text renderable for mock
        return new Text($"[Task Panel: {title}]");
    }

    /// <inheritdoc />
    public IRenderable RenderContextPanel(IReadOnlyDictionary<string, string> data, string title = "Context")
    {
        _contextPanelCalls.Add(new ContextPanelCall
        {
            Data = new Dictionary<string, string>(data),
            Title = title
        });

        // Return a simple text renderable for mock
        return new Text($"[Context Panel: {title}]");
    }

    /// <summary>
    /// Clears all recorded calls.
    /// </summary>
    public void Reset()
    {
        _splitLayoutCalls.Clear();
        _taskPanelCalls.Clear();
        _contextPanelCalls.Clear();
    }

    /// <summary>
    /// Record of a split layout render call.
    /// </summary>
    public record SplitLayoutCall
    {
        public IRenderable? MainContent { get; init; }
        public IRenderable? SidePanel { get; init; }
        public SplitLayoutConfig Config { get; init; } = new();
        public int TerminalWidth { get; init; }
        public bool WasSplit { get; init; }
    }

    /// <summary>
    /// Record of a task panel render call.
    /// </summary>
    public record TaskPanelCall
    {
        public IReadOnlyList<TaskItem> Tasks { get; init; } = Array.Empty<TaskItem>();
        public string Title { get; init; } = string.Empty;
    }

    /// <summary>
    /// Record of a context panel render call.
    /// </summary>
    public record ContextPanelCall
    {
        public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
        public string Title { get; init; } = string.Empty;
    }
}
