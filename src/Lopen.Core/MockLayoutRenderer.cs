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
    private readonly List<LiveLayoutCall> _liveLayoutCalls = new();

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

    /// <summary>
    /// Gets all live layout start calls.
    /// </summary>
    public IReadOnlyList<LiveLayoutCall> LiveLayoutCalls => _liveLayoutCalls.AsReadOnly();

    /// <summary>
    /// Gets the last created mock live context for testing.
    /// </summary>
    public MockLiveLayoutContext? LastLiveContext { get; private set; }

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
        _liveLayoutCalls.Clear();
        LastLiveContext = null;
    }

    /// <inheritdoc />
    public Task<ILiveLayoutContext> StartLiveLayoutAsync(
        IRenderable initialMain,
        IRenderable? initialPanel = null,
        SplitLayoutConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new SplitLayoutConfig();

        _liveLayoutCalls.Add(new LiveLayoutCall
        {
            InitialMain = initialMain,
            InitialPanel = initialPanel,
            Config = config,
            TerminalWidth = SimulatedWidth
        });

        LastLiveContext = new MockLiveLayoutContext();
        return Task.FromResult<ILiveLayoutContext>(LastLiveContext);
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

    /// <summary>
    /// Record of a live layout start call.
    /// </summary>
    public record LiveLayoutCall
    {
        public IRenderable? InitialMain { get; init; }
        public IRenderable? InitialPanel { get; init; }
        public SplitLayoutConfig Config { get; init; } = new();
        public int TerminalWidth { get; init; }
    }
}

/// <summary>
/// Mock live layout context for testing.
/// Records all updates and provides control over context lifecycle.
/// </summary>
public class MockLiveLayoutContext : ILiveLayoutContext
{
    private readonly List<IRenderable> _mainUpdates = new();
    private readonly List<IRenderable> _panelUpdates = new();
    private volatile bool _isActive = true;
    private int _refreshCount;
    private int _simulatedMainUpdateCount;

    /// <summary>
    /// Gets all main content updates recorded.
    /// </summary>
    public IReadOnlyList<IRenderable> MainUpdates => _mainUpdates.AsReadOnly();

    /// <summary>
    /// Gets all panel content updates recorded.
    /// </summary>
    public IReadOnlyList<IRenderable> PanelUpdates => _panelUpdates.AsReadOnly();

    /// <summary>
    /// Gets the number of times Refresh was called.
    /// </summary>
    public int RefreshCount => _refreshCount;

    /// <summary>
    /// Gets the number of simulated main updates (used by MockStreamRenderer).
    /// </summary>
    public int SimulatedMainUpdateCount => _simulatedMainUpdateCount;

    /// <inheritdoc />
    public bool IsActive => _isActive;

    /// <inheritdoc />
    public void UpdateMain(IRenderable content)
    {
        if (_isActive)
        {
            _mainUpdates.Add(content);
        }
    }

    /// <inheritdoc />
    public void UpdatePanel(IRenderable content)
    {
        if (_isActive)
        {
            _panelUpdates.Add(content);
        }
    }

    /// <inheritdoc />
    public void Refresh()
    {
        if (_isActive)
        {
            Interlocked.Increment(ref _refreshCount);
        }
    }

    /// <summary>
    /// Simulates a main content update (called by MockStreamRenderer).
    /// </summary>
    public void SimulateMainUpdate()
    {
        if (_isActive)
        {
            Interlocked.Increment(ref _simulatedMainUpdateCount);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _isActive = false;
        return ValueTask.CompletedTask;
    }
}
