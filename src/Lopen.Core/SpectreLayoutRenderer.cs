using Spectre.Console;
using Spectre.Console.Rendering;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of layout renderer.
/// Displays split layouts with responsive behavior.
/// Respects NO_COLOR environment variable.
/// </summary>
public class SpectreLayoutRenderer : ILayoutRenderer
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;

    public SpectreLayoutRenderer()
        : this(AnsiConsole.Console)
    {
    }

    public SpectreLayoutRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    /// <inheritdoc />
    public int TerminalWidth => _console.Profile.Width;

    /// <inheritdoc />
    public void RenderSplitLayout(
        IRenderable mainContent,
        IRenderable? sidePanel = null,
        SplitLayoutConfig? config = null)
    {
        config ??= new SplitLayoutConfig();

        // Check if terminal is wide enough for split
        if (TerminalWidth < config.MinWidthForSplit || sidePanel == null)
        {
            // Fallback: Full-width main content only
            _console.Write(mainContent);
            return;
        }

        // Create split layout
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("Main").Ratio(config.MainRatio),
                new Layout("Panel").Ratio(config.PanelRatio)
            );

        layout["Main"].Update(mainContent);
        layout["Panel"].Update(sidePanel);

        _console.Write(layout);
    }

    /// <inheritdoc />
    public IRenderable RenderTaskPanel(IReadOnlyList<TaskItem> tasks, string title = "Progress")
    {
        var content = new List<string>();

        foreach (var task in tasks)
        {
            var (symbol, style) = GetTaskSymbolAndStyle(task.Status);
            if (_useColors)
            {
                content.Add($"[{style}]{symbol}[/] {Markup.Escape(task.Name)}");
            }
            else
            {
                content.Add($"{symbol} {task.Name}");
            }
        }

        var text = string.Join("\n", content);
        var markup = _useColors ? new Markup(text) : new Markup(Markup.Escape(text.Replace("[", "[[").Replace("]", "]]")));

        return CreatePanel(markup, title);
    }

    /// <inheritdoc />
    public IRenderable RenderContextPanel(IReadOnlyDictionary<string, string> data, string title = "Context")
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());

        foreach (var (key, value) in data)
        {
            if (_useColors)
            {
                grid.AddRow(
                    $"[bold]{Markup.Escape(key)}:[/]",
                    Markup.Escape(value)
                );
            }
            else
            {
                grid.AddRow(
                    $"{Markup.Escape(key)}:",
                    Markup.Escape(value)
                );
            }
        }

        return CreatePanel(grid, title);
    }

    private Panel CreatePanel(IRenderable content, string title)
    {
        if (_useColors && _console.Profile.Capabilities.Interactive)
        {
            return new Panel(content)
            {
                Header = new PanelHeader($" {Markup.Escape(title)} "),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Blue),
                Padding = new Padding(1, 0, 1, 0)
            };
        }

        // ASCII fallback
        return new Panel(content)
        {
            Header = new PanelHeader($" {title} "),
            Border = BoxBorder.Ascii,
            Padding = new Padding(1, 0, 1, 0)
        };
    }

    private static (string Symbol, string Style) GetTaskSymbolAndStyle(TaskStatus status) => status switch
    {
        TaskStatus.Completed => ("✓", "green"),
        TaskStatus.InProgress => ("⏳", "yellow"),
        TaskStatus.Failed => ("✗", "red"),
        _ => ("○", "dim")
    };

    /// <inheritdoc />
    public async Task<ILiveLayoutContext> StartLiveLayoutAsync(
        IRenderable initialMain,
        IRenderable? initialPanel = null,
        SplitLayoutConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new SplitLayoutConfig();
        var context = new SpectreLiveLayoutContext(_console, initialMain, initialPanel, config);
        await context.StartAsync(cancellationToken);
        return context;
    }
}

/// <summary>
/// Live layout context using Spectre.Console's Live display.
/// Enables non-blocking updates to main content and side panel.
/// </summary>
public sealed class SpectreLiveLayoutContext : ILiveLayoutContext
{
    private readonly IAnsiConsole _console;
    private readonly SplitLayoutConfig _config;
    private readonly Layout _layout;
    private LiveDisplayContext? _liveContext;
    private readonly TaskCompletionSource _startedTcs = new();
    private CancellationTokenSource? _cts;
    private Task? _displayTask;
    private volatile bool _isActive;

    internal SpectreLiveLayoutContext(
        IAnsiConsole console,
        IRenderable initialMain,
        IRenderable? initialPanel,
        SplitLayoutConfig config)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _config = config ?? new SplitLayoutConfig();

        // Create layout based on terminal width and config
        if (_console.Profile.Width >= _config.MinWidthForSplit && initialPanel != null)
        {
            _layout = new Layout("Root")
                .SplitColumns(
                    new Layout("Main").Ratio(_config.MainRatio),
                    new Layout("Panel").Ratio(_config.PanelRatio)
                );
            _layout["Main"].Update(initialMain);
            _layout["Panel"].Update(initialPanel);
        }
        else
        {
            _layout = new Layout("Root");
            _layout.Update(initialMain);
        }
    }

    /// <inheritdoc />
    public bool IsActive => _isActive;

    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _displayTask = Task.Run(async () =>
        {
            await _console.Live(_layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    _liveContext = ctx;
                    _isActive = true;
                    _startedTcs.TrySetResult();

                    try
                    {
                        // Keep alive until disposed
                        await Task.Delay(Timeout.Infinite, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected on dispose
                    }
                });
        }, _cts.Token);

        // Wait for the live context to be ready
        await _startedTcs.Task;
    }

    /// <inheritdoc />
    public void UpdateMain(IRenderable content)
    {
        if (!_isActive || _layout == null) return;

        if (_layout["Main"] != null)
        {
            _layout["Main"].Update(content);
        }
        else
        {
            _layout.Update(content);
        }
    }

    /// <inheritdoc />
    public void UpdatePanel(IRenderable content)
    {
        if (!_isActive || _layout == null) return;

        if (_layout["Panel"] != null)
        {
            _layout["Panel"].Update(content);
        }
    }

    /// <inheritdoc />
    public void Refresh()
    {
        if (_isActive)
        {
            _liveContext?.Refresh();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _isActive = false;

        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        if (_displayTask != null)
        {
            try
            {
                await _displayTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }
}
