using RadLine;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Wraps RadLine's LineEditor with Lopen-specific configuration:
/// themed prompt, multi-line mode, history, and slash command completion.
/// </summary>
public sealed class LopenLineEditor
{
    private readonly LineEditor _editor;

    public LopenLineEditor(
        IAnsiConsole console,
        ILineEditorHistory history,
        ITextCompletion? completion = null)
    {
        // RadLine's History property is read-only; inject via IServiceProvider
        var provider = new HistoryServiceProvider(history);

        _editor = new LineEditor(console, provider: provider)
        {
            Prompt = new LineEditorPrompt($"[{LopenTheme.Primary.ToMarkup()} bold]{LopenTheme.PromptChar}[/]"),
            MultiLine = true,
            Completion = completion,
        };
    }

    /// <summary>
    /// Reads a line of input from the user.
    /// Returns null if the user cancels (Ctrl+C).
    /// </summary>
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        return _editor.ReadLine(cancellationToken);
    }

    /// <summary>
    /// Minimal service provider that resolves ILineEditorHistory for RadLine.
    /// </summary>
    private sealed class HistoryServiceProvider(ILineEditorHistory history) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(ILineEditorHistory) ? history : null;
        }
    }
}
