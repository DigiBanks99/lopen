using Lopen.Core;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// TUI implementation of IOutputRenderer using Spectre.Console.
/// Renders progress, errors, results, and prompts with themed styling.
/// </summary>
public sealed class TuiOutputRenderer(IAnsiConsole console, Lazy<LopenLineEditor> lineEditor) : IOutputRenderer
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
    private readonly Lazy<LopenLineEditor> _lineEditor = lineEditor ?? throw new ArgumentNullException(nameof(lineEditor));

    public Task RenderProgressAsync(string phase, string step, double progress, CancellationToken cancellationToken = default)
    {
        string phaseText = LopenTheme.Bold(phase, LopenTheme.Secondary);
        string stepText = LopenTheme.Styled(step, LopenTheme.Muted);
        string pct = progress >= 0 ? $" ({progress:P0})" : "";
        _console.MarkupLine($"{LopenTheme.SectionMarker} {phaseText} {stepText}{Markup.Escape(pct)}");
        return Task.CompletedTask;
    }

    public Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        Panel panel = new Panel(Markup.Escape(message))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(LopenTheme.Error),
            Header = new PanelHeader(LopenTheme.Bold("Error", LopenTheme.Error)),
        };

        _console.Write(panel);

        if (exception is not null)
        {
            _console.MarkupLine(LopenTheme.Dim($"  {exception.GetType().Name}: {exception.Message}", LopenTheme.Muted));
        }

        return Task.CompletedTask;
    }

    public Task RenderResultAsync(string message, CancellationToken cancellationToken = default)
    {
        _console.MarkupLine(Markup.Escape(message));
        return Task.CompletedTask;
    }

    public async Task<string?> PromptAsync(string message, CancellationToken cancellationToken = default)
    {
        _console.MarkupLine(Markup.Escape(message));
        return await _lineEditor.Value.ReadLineAsync(cancellationToken);
    }
}
