using Lopen.Core;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// TUI implementation of IOutputRenderer using Spectre.Console.
/// Renders progress, errors, results, and prompts with themed styling.
/// </summary>
public sealed class TuiOutputRenderer : IOutputRenderer
{
    private readonly IAnsiConsole _console;
    private readonly LopenLineEditor _lineEditor;

    public TuiOutputRenderer(IAnsiConsole console, LopenLineEditor lineEditor)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _lineEditor = lineEditor ?? throw new ArgumentNullException(nameof(lineEditor));
    }

    public Task RenderProgressAsync(string phase, string step, double progress, CancellationToken cancellationToken = default)
    {
        var phaseText = LopenTheme.Bold(phase, LopenTheme.Secondary);
        var stepText = LopenTheme.Styled(step, LopenTheme.Muted);
        var pct = progress >= 0 ? $" ({progress:P0})" : "";
        _console.MarkupLine($"{LopenTheme.SectionMarker} {phaseText} {stepText}{Markup.Escape(pct)}");
        return Task.CompletedTask;
    }

    public Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        var panel = new Panel(Markup.Escape(message))
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
        return await _lineEditor.ReadLineAsync(cancellationToken);
    }
}
