using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Tui.Commands;
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
    private readonly SlashCommandRegistry _commandRegistry;
    private readonly IWorkflowOrchestrator? _orchestrator;

    public TuiRunner(
        IAnsiConsole console,
        LopenLineEditor lineEditor,
        TuiUserPromptQueue promptQueue,
        IOutputRenderer renderer,
        SlashCommandRegistry commandRegistry,
        IWorkflowOrchestrator? orchestrator = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _lineEditor = lineEditor ?? throw new ArgumentNullException(nameof(lineEditor));
        _promptQueue = promptQueue ?? throw new ArgumentNullException(nameof(promptQueue));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
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

            // Dispatch slash commands
            if (input.StartsWith('/'))
            {
                var result = await _commandRegistry.DispatchAsync(input, cancellationToken);
                if (result == SlashCommandResult.ExitRequested)
                    return 0;
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
