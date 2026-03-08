using Lopen.Core;
using Lopen.Core.Workflow;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Runs the TUI REPL loop: renders overview, reads input, dispatches commands or enqueues prompts.
/// </summary>
public sealed class TuiRunner
{
    private readonly IAnsiConsole _console;
    private readonly LopenLineEditor _lineEditor;
    private readonly TuiUserPromptQueue _promptQueue;
    private readonly IOutputRenderer _renderer;
    private readonly IWorkflowOrchestrator? _orchestrator;

    public TuiRunner(
        IAnsiConsole console,
        LopenLineEditor lineEditor,
        TuiUserPromptQueue promptQueue,
        IOutputRenderer renderer,
        IWorkflowOrchestrator? orchestrator = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _lineEditor = lineEditor ?? throw new ArgumentNullException(nameof(lineEditor));
        _promptQueue = promptQueue ?? throw new ArgumentNullException(nameof(promptQueue));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Runs the TUI REPL loop until the user exits.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? input;
            try
            {
                input = await _lineEditor.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Ctrl+C returns null
            if (input is null)
                continue;

            // Empty input - just re-prompt
            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Handle /exit command
            if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                _console.MarkupLine(LopenTheme.Styled("Goodbye!", LopenTheme.Muted));
                return 0;
            }

            // Handle slash commands (basic dispatch for now)
            if (input.StartsWith('/'))
            {
                _console.MarkupLine(LopenTheme.Styled(
                    $"Unknown command: {input}. Type ? for available commands.", LopenTheme.Warning));
                continue;
            }

            // Enqueue user input for the orchestrator
            _promptQueue.Enqueue(input);

            // If we have an orchestrator but no workflow running, we'd start one here
            // For now, acknowledge the input
            _console.MarkupLine(LopenTheme.Dim("(Prompt enqueued)", LopenTheme.Muted));
        }

        return 0;
    }
}
