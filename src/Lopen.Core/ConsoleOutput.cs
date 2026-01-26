using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Helper for styled console output using Spectre.Console.
/// Respects NO_COLOR environment variable.
/// </summary>
public class ConsoleOutput
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;

    public ConsoleOutput() : this(AnsiConsole.Console)
    {
    }

    public ConsoleOutput(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    /// <summary>
    /// Write a success message (green).
    /// </summary>
    public void Success(string message)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
        }
        else
        {
            _console.WriteLine($"✓ {message}");
        }
    }

    /// <summary>
    /// Write an error message (red).
    /// </summary>
    public void Error(string message)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
        }
        else
        {
            _console.WriteLine($"✗ {message}");
        }
    }

    /// <summary>
    /// Write a warning message (yellow).
    /// </summary>
    public void Warning(string message)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[yellow]![/] {Markup.Escape(message)}");
        }
        else
        {
            _console.WriteLine($"! {message}");
        }
    }

    /// <summary>
    /// Write an info message (blue).
    /// </summary>
    public void Info(string message)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
        }
        else
        {
            _console.WriteLine($"ℹ {message}");
        }
    }

    /// <summary>
    /// Write a muted message (gray).
    /// </summary>
    public void Muted(string message)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
        }
        else
        {
            _console.WriteLine(message);
        }
    }

    /// <summary>
    /// Write a plain message.
    /// </summary>
    public void WriteLine(string message)
    {
        _console.WriteLine(message);
    }

    /// <summary>
    /// Write an empty line.
    /// </summary>
    public void WriteLine()
    {
        _console.WriteLine();
    }

    /// <summary>
    /// Write text without newline (for prompts).
    /// </summary>
    public void Write(string text)
    {
        _console.Write(new Text(text));
    }

    /// <summary>
    /// Write a key-value pair.
    /// </summary>
    public void KeyValue(string key, string value)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[bold]{Markup.Escape(key)}:[/] {Markup.Escape(value)}");
        }
        else
        {
            _console.WriteLine($"{key}: {value}");
        }
    }

    /// <summary>
    /// Write a horizontal rule with centered text.
    /// </summary>
    public void Rule(string? title = null)
    {
        if (string.IsNullOrEmpty(title))
        {
            _console.Write(new Rule());
        }
        else if (_useColors)
        {
            _console.Write(new Rule($"[bold cyan]{Markup.Escape(title)}[/]"));
        }
        else
        {
            _console.Write(new Rule(title));
        }
    }

    /// <summary>
    /// Show a spinner while executing an async operation.
    /// </summary>
    public async Task<T> ShowStatusAsync<T>(
        string status,
        Func<Task<T>> operation,
        SpinnerType spinnerType = SpinnerType.Dots)
    {
        var renderer = new SpectreProgressRenderer(_console, spinnerType);
        return await renderer.ShowProgressAsync(status, async _ => await operation());
    }

    /// <summary>
    /// Show a spinner while executing an async operation.
    /// </summary>
    public async Task ShowStatusAsync(
        string status,
        Func<Task> operation,
        SpinnerType spinnerType = SpinnerType.Dots)
    {
        var renderer = new SpectreProgressRenderer(_console, spinnerType);
        await renderer.ShowProgressAsync(status, async _ => await operation());
    }

    /// <summary>
    /// Write an error with a suggestion.
    /// </summary>
    public void ErrorWithSuggestion(string message, string suggestion)
    {
        var renderer = new SpectreErrorRenderer(_console);
        renderer.RenderSimpleError(message, suggestion);
    }

    /// <summary>
    /// Write an error in a bordered panel with suggestions.
    /// </summary>
    public void ErrorPanel(string title, string message, params string[] suggestions)
    {
        var renderer = new SpectreErrorRenderer(_console);
        renderer.RenderPanelError(title, message, suggestions);
    }

    /// <summary>
    /// Write a command not found error with suggestions.
    /// </summary>
    public void CommandNotFoundError(string command, params string[] suggestions)
    {
        var renderer = new SpectreErrorRenderer(_console);
        renderer.RenderCommandNotFound(command, suggestions);
    }

    /// <summary>
    /// Write a validation error with valid options.
    /// </summary>
    public void ValidationError(string input, string message, params string[] validOptions)
    {
        var renderer = new SpectreErrorRenderer(_console);
        renderer.RenderValidationError(input, message, validOptions);
    }

    /// <summary>
    /// Render a table of items.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="items">Items to display.</param>
    /// <param name="config">Table configuration.</param>
    public void Table<T>(IEnumerable<T> items, TableConfig<T> config)
    {
        var renderer = new SpectreDataRenderer(_console);
        renderer.RenderTable(items, config);
    }

    /// <summary>
    /// Render key-value metadata in a panel.
    /// </summary>
    /// <param name="data">Key-value pairs to display.</param>
    /// <param name="title">Panel title.</param>
    public void Metadata(IReadOnlyDictionary<string, string> data, string title)
    {
        var renderer = new SpectreDataRenderer(_console);
        renderer.RenderMetadata(data, title);
    }

    /// <summary>
    /// Render a split layout with main content and optional side panel.
    /// Falls back to full-width main content if terminal is too narrow.
    /// </summary>
    /// <param name="mainContent">The main content to display.</param>
    /// <param name="sidePanel">Optional side panel content.</param>
    /// <param name="config">Layout configuration (optional).</param>
    public void SplitLayout(
        Spectre.Console.Rendering.IRenderable mainContent,
        Spectre.Console.Rendering.IRenderable? sidePanel = null,
        SplitLayoutConfig? config = null)
    {
        var renderer = new SpectreLayoutRenderer(_console);
        renderer.RenderSplitLayout(mainContent, sidePanel, config);
    }

    /// <summary>
    /// Render a task progress panel.
    /// </summary>
    /// <param name="tasks">Tasks to display.</param>
    /// <param name="title">Panel title.</param>
    /// <returns>A renderable panel containing the task list.</returns>
    public Spectre.Console.Rendering.IRenderable TaskPanel(IReadOnlyList<TaskItem> tasks, string title = "Progress")
    {
        var renderer = new SpectreLayoutRenderer(_console);
        return renderer.RenderTaskPanel(tasks, title);
    }

    /// <summary>
    /// Render a context panel with key-value metadata.
    /// </summary>
    /// <param name="data">Key-value pairs to display.</param>
    /// <param name="title">Panel title.</param>
    /// <returns>A renderable panel containing the metadata.</returns>
    public Spectre.Console.Rendering.IRenderable ContextPanel(IReadOnlyDictionary<string, string> data, string title = "Context")
    {
        var renderer = new SpectreLayoutRenderer(_console);
        return renderer.RenderContextPanel(data, title);
    }
}
