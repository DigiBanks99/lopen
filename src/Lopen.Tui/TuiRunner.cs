using System.Runtime.InteropServices;
using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Runs the TUI REPL loop: renders overview, reads input, dispatches commands or enqueues prompts.
/// Implements two-tier cancellation: per-turn CTS for step cancellation and global exit on double Ctrl+C.
/// </summary>
public sealed class TuiRunner
{
    private readonly IAnsiConsole _console;
    private readonly LopenLineEditor _lineEditor;
    private readonly TuiUserPromptQueue _promptQueue;
    private readonly IOutputRenderer _renderer;
    private readonly SlashCommandRegistry _commandRegistry;
    private readonly IWorkflowOrchestrator? _orchestrator;
    private readonly WorkflowOverviewBlock? _overviewBlock;

    public TuiRunner(
        IAnsiConsole console,
        LopenLineEditor lineEditor,
        TuiUserPromptQueue promptQueue,
        IOutputRenderer renderer,
        SlashCommandRegistry commandRegistry,
        IWorkflowOrchestrator? orchestrator = null,
        WorkflowOverviewBlock? overviewBlock = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _lineEditor = lineEditor ?? throw new ArgumentNullException(nameof(lineEditor));
        _promptQueue = promptQueue ?? throw new ArgumentNullException(nameof(promptQueue));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _orchestrator = orchestrator;
        _overviewBlock = overviewBlock;
    }

    /// <summary>
    /// Runs the TUI REPL loop until the user exits.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _overviewBlock?.Render();
            string? input;
            try
            {
                input = await _lineEditor.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Ctrl+D on empty prompt — exit gracefully
            if (_lineEditor.ExitRequested)
                return 0;

            // Ctrl+C returns null — re-prompt
            if (input is null)
                continue;

            // Empty input — just re-prompt
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

            // Enqueue and process user input
            _promptQueue.Enqueue(input);
            await ProcessTurnAsync(cancellationToken);
        }

        return 0;
    }

    /// <summary>
    /// Processes a single orchestrator turn with per-turn cancellation.
    /// First Ctrl+C during processing cancels the current turn; a second Ctrl+C exits.
    /// </summary>
    internal async Task ProcessTurnAsync(CancellationToken globalToken)
    {
        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);

        using var sigintRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGINT,
            _ => HandleCancelDuringProcessing(turnCts));

        await ExecuteTurnAsync(turnCts, globalToken);
    }

    /// <summary>
    /// Core turn execution logic, separated from signal registration for testability.
    /// </summary>
    internal async Task ExecuteTurnAsync(CancellationTokenSource turnCts, CancellationToken globalToken)
    {
        try
        {
            if (_orchestrator is not null)
            {
                await _orchestrator.RunStepAsync(string.Empty, cancellationToken: turnCts.Token);
            }
            else
            {
                _console.MarkupLine(LopenTheme.Dim("(Prompt enqueued)", LopenTheme.Muted));
            }
        }
        catch (OperationCanceledException) when (turnCts.IsCancellationRequested && !globalToken.IsCancellationRequested)
        {
            _console.MarkupLine(LopenTheme.Styled($"{LopenTheme.InfoHint} Cancelled.", LopenTheme.Warning));
        }
    }

    internal static void HandleCancelDuringProcessing(CancellationTokenSource turnCts)
    {
        if (turnCts.IsCancellationRequested)
        {
            // Second Ctrl+C during processing — let the signal propagate (force exit)
            return;
        }

        // First Ctrl+C during processing — cancel current turn only
        try { turnCts.Cancel(); } catch (ObjectDisposedException) { }
    }
}
