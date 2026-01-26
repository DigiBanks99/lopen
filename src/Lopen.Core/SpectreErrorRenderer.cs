using System.Text;
using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of error renderer.
/// Respects NO_COLOR environment variable.
/// </summary>
public class SpectreErrorRenderer : IErrorRenderer
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;

    public SpectreErrorRenderer()
        : this(AnsiConsole.Console)
    {
    }

    public SpectreErrorRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    public void RenderSimpleError(string message, string? suggestion = null)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
            if (!string.IsNullOrEmpty(suggestion))
            {
                _console.MarkupLine($"  [dim]💡 Try:[/] [blue]{Markup.Escape(suggestion)}[/]");
            }
        }
        else
        {
            _console.WriteLine($"✗ {message}");
            if (!string.IsNullOrEmpty(suggestion))
            {
                _console.WriteLine($"  Try: {suggestion}");
            }
        }
    }

    public void RenderPanelError(string title, string message, IEnumerable<string>? suggestions = null)
    {
        var suggestionList = suggestions?.ToList() ?? new List<string>();

        if (_useColors)
        {
            var content = new StringBuilder();
            content.AppendLine(Markup.Escape(message));

            if (suggestionList.Count > 0)
            {
                content.AppendLine();
                content.AppendLine("[yellow]Suggestions:[/]");
                foreach (var suggestion in suggestionList)
                {
                    content.AppendLine($"  • {Markup.Escape(suggestion)}");
                }
            }

            var panel = new Panel(content.ToString().TrimEnd())
            {
                Header = new PanelHeader($"[red]✗ Error: {Markup.Escape(title)}[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red),
                Padding = new Padding(1, 0, 1, 0)
            };

            _console.Write(panel);
        }
        else
        {
            _console.WriteLine($"--- Error: {title} ---");
            _console.WriteLine(message);
            if (suggestionList.Count > 0)
            {
                _console.WriteLine();
                _console.WriteLine("Suggestions:");
                foreach (var suggestion in suggestionList)
                {
                    _console.WriteLine($"  * {suggestion}");
                }
            }
            _console.WriteLine("---");
        }
    }

    public void RenderValidationError(string input, string message, IEnumerable<string> validOptions)
    {
        var options = validOptions?.ToList() ?? new List<string>();

        if (_useColors)
        {
            _console.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
            _console.WriteLine();
            _console.MarkupLine($"  [dim]{Markup.Escape(input)}[/]");
            _console.MarkupLine($"  [red]^[/]");
            _console.WriteLine();
            if (options.Count > 0)
            {
                _console.MarkupLine($"  [dim]💡 Valid options:[/] [cyan]{Markup.Escape(string.Join(", ", options))}[/]");
            }
        }
        else
        {
            _console.WriteLine($"⚠ {message}");
            _console.WriteLine();
            _console.WriteLine($"  {input}");
            _console.WriteLine("  ^");
            _console.WriteLine();
            if (options.Count > 0)
            {
                _console.WriteLine($"  Valid options: {string.Join(", ", options)}");
            }
        }
    }

    public void RenderCommandNotFound(string command, IEnumerable<string> suggestions)
    {
        var suggestionList = suggestions?.ToList() ?? new List<string>();

        if (_useColors)
        {
            var content = new StringBuilder();
            content.AppendLine($"Command '[cyan]{Markup.Escape(command)}[/]' not found");

            if (suggestionList.Count > 0)
            {
                content.AppendLine();
                content.AppendLine("[yellow]Did you mean?[/]");
                foreach (var suggestion in suggestionList)
                {
                    content.AppendLine($"  • [cyan]{Markup.Escape(suggestion)}[/]");
                }
            }

            content.AppendLine();
            content.AppendLine("[dim]Run 'lopen --help' for available commands[/]");

            var panel = new Panel(content.ToString().TrimEnd())
            {
                Header = new PanelHeader("[red]✗ Invalid command[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red),
                Padding = new Padding(1, 0, 1, 0)
            };

            _console.Write(panel);
        }
        else
        {
            _console.WriteLine("--- Invalid command ---");
            _console.WriteLine($"Command '{command}' not found");
            if (suggestionList.Count > 0)
            {
                _console.WriteLine();
                _console.WriteLine("Did you mean?");
                foreach (var suggestion in suggestionList)
                {
                    _console.WriteLine($"  * {suggestion}");
                }
            }
            _console.WriteLine();
            _console.WriteLine("Run 'lopen --help' for available commands");
            _console.WriteLine("---");
        }
    }

    public void RenderError(ErrorInfo error)
    {
        if (_useColors)
        {
            var content = new StringBuilder();
            content.AppendLine(Markup.Escape(error.Message));

            if (!string.IsNullOrEmpty(error.DidYouMean))
            {
                content.AppendLine();
                content.AppendLine($"[yellow]Did you mean?[/] [cyan]{Markup.Escape(error.DidYouMean)}[/]");
            }

            if (error.Suggestions.Count > 0)
            {
                content.AppendLine();
                content.AppendLine("[yellow]Suggestions:[/]");
                foreach (var suggestion in error.Suggestions)
                {
                    content.AppendLine($"  • {Markup.Escape(suggestion)}");
                }
            }

            if (!string.IsNullOrEmpty(error.TryCommand))
            {
                content.AppendLine();
                content.AppendLine($"[dim]💡 Try:[/] [blue]{Markup.Escape(error.TryCommand)}[/]");
            }

            var symbol = error.Severity == ErrorSeverity.Warning ? "⚠" : "✗";
            var color = error.Severity == ErrorSeverity.Warning ? Color.Yellow : Color.Red;

            var panel = new Panel(content.ToString().TrimEnd())
            {
                Header = new PanelHeader($"[{color}]{symbol} {Markup.Escape(error.Title)}[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(color),
                Padding = new Padding(1, 0, 1, 0)
            };

            _console.Write(panel);
        }
        else
        {
            var symbol = error.Severity == ErrorSeverity.Warning ? "⚠" : "✗";
            _console.WriteLine($"--- {symbol} {error.Title} ---");
            _console.WriteLine(error.Message);

            if (!string.IsNullOrEmpty(error.DidYouMean))
            {
                _console.WriteLine();
                _console.WriteLine($"Did you mean? {error.DidYouMean}");
            }

            if (error.Suggestions.Count > 0)
            {
                _console.WriteLine();
                _console.WriteLine("Suggestions:");
                foreach (var suggestion in error.Suggestions)
                {
                    _console.WriteLine($"  * {suggestion}");
                }
            }

            if (!string.IsNullOrEmpty(error.TryCommand))
            {
                _console.WriteLine();
                _console.WriteLine($"Try: {error.TryCommand}");
            }
            _console.WriteLine("---");
        }
    }
}
