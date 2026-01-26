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
}
