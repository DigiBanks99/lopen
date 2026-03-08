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
    private readonly ExitRequestedFlag _exitFlag = new();

    public LopenLineEditor(
        IAnsiConsole console,
        ILineEditorHistory history,
        ITextCompletion? completion = null)
    {
        var provider = new EditorServiceProvider(history, _exitFlag);

        _editor = new LineEditor(console, provider: provider)
        {
            Prompt = new LineEditorPrompt($"[{LopenTheme.Primary.ToMarkup()} bold]{LopenTheme.PromptChar}[/]"),
            MultiLine = true,
            Completion = completion,
        };

        // Ctrl+D: exit on empty prompt
        _editor.KeyBindings.Add(ConsoleKey.D, ConsoleModifiers.Control, () => new ExitOnEmptyCommand(_exitFlag));
    }

    /// <summary>
    /// Indicates Ctrl+D was pressed on an empty prompt, signaling the TUI should exit.
    /// </summary>
    public bool ExitRequested => _exitFlag.IsSet;

    /// <summary>
    /// Reads a line of input from the user.
    /// Returns null if the user cancels (Ctrl+C) or exits (Ctrl+D on empty).
    /// </summary>
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        return _editor.ReadLine(cancellationToken);
    }

    /// <summary>
    /// Shared flag set by <see cref="ExitOnEmptyCommand"/> to signal an exit request.
    /// </summary>
    internal sealed class ExitRequestedFlag
    {
        public bool IsSet { get; set; }
    }

    /// <summary>
    /// Minimal service provider that resolves ILineEditorHistory and ExitRequestedFlag for RadLine.
    /// </summary>
    private sealed class EditorServiceProvider(ILineEditorHistory history, ExitRequestedFlag exitFlag) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ILineEditorHistory)) return history;
            if (serviceType == typeof(ExitRequestedFlag)) return exitFlag;
            return null;
        }
    }
}
