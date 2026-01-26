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
}
