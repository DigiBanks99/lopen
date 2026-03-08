using System.Runtime.InteropServices;
using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Runs the TUI REPL loop: renders overview, reads input, dispatches commands or enqueues prompts.
/// Implements two-tier cancellation: per-turn CTS for step cancellation and global exit on double Ctrl+C.
/// </summary>
public sealed class TuiRunner(
    IAnsiConsole console,
    LopenLineEditor lineEditor,
    TuiUserPromptQueue promptQueue,
    IOutputRenderer renderer,
    SlashCommandRegistry commandRegistry,
    IWorkflowOrchestrator? orchestrator = null,
    WorkflowOverviewBlock? overviewBlock = null,
    CommandPalette? commandPalette = null,
    ISessionManager? sessionManager = null)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
    private readonly LopenLineEditor _lineEditor = lineEditor ?? throw new ArgumentNullException(nameof(lineEditor));
    private readonly TuiUserPromptQueue _promptQueue = promptQueue ?? throw new ArgumentNullException(nameof(promptQueue));
    private readonly IOutputRenderer _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    private readonly SlashCommandRegistry _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
    private readonly IWorkflowOrchestrator? _orchestrator = orchestrator;
    private readonly WorkflowOverviewBlock? _overviewBlock = overviewBlock;
    private readonly CommandPalette? _commandPalette = commandPalette;
    private readonly ISessionManager? _sessionManager = sessionManager;

    /// <summary>
    /// Runs the TUI REPL loop until the user exits.
    /// </summary>
    public async Task<int> RunAsync(SessionId? initialSessionId = null, CancellationToken cancellationToken = default)
    {
        await HandleStartupSessionDetectionAsync(initialSessionId, cancellationToken);

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

            // Ctrl+O was pressed — open command palette (TUI-11)
            if (_lineEditor.CommandPaletteRequested)
            {
                _lineEditor.ResetCommandPaletteRequested();
                if (await ShowCommandPaletteAsync(cancellationToken))
                    return 0;
                continue;
            }

            // Ctrl+C returns null — re-prompt
            if (input is null)
                continue;

            // Empty input — just re-prompt
            if (string.IsNullOrWhiteSpace(input))
                continue;

            // ? on empty prompt — open command palette (TUI-10)
            if (input.Trim() == "?" && _commandPalette is not null)
            {
                if (await ShowCommandPaletteAsync(cancellationToken))
                    return 0;
                continue;
            }

            // Dispatch slash commands
            if (input.StartsWith('/'))
            {
                SlashCommandResult result = await _commandRegistry.DispatchAsync(input, cancellationToken);
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
    /// Detects open sessions on startup and either auto-resumes or shows a hint line.
    /// </summary>
    internal async Task HandleStartupSessionDetectionAsync(SessionId? initialSessionId, CancellationToken cancellationToken)
    {
        // Auto-resume: --resume flag resolved a session before TuiRunner started
        if (initialSessionId is not null && _orchestrator is not null && _sessionManager is not null)
        {
            SessionState? state = await _sessionManager.LoadSessionStateAsync(initialSessionId, cancellationToken);
            if (state is not null)
            {
                await _orchestrator.InitializeAsync(state.Module, initialSessionId, cancellationToken);
                _console.MarkupLine(LopenTheme.Styled(
                    $"{LopenTheme.InfoHint} Resuming session {initialSessionId} at {state.Phase}/{state.Step}",
                    LopenTheme.Accent));
            }
            else
            {
                _console.MarkupLine(LopenTheme.Styled(
                    $"Session {initialSessionId} not found or corrupted. Starting fresh.",
                    LopenTheme.Warning));
            }

            return;
        }

        // Hint line: notify user about incomplete sessions
        if (_sessionManager is not null)
        {
            IReadOnlyList<SessionId> sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

            foreach (SessionId session in sessions)
            {
                SessionState? state = await _sessionManager.LoadSessionStateAsync(session, cancellationToken);
                if (state is not null && !state.IsComplete)
                {
                    _console.MarkupLine(LopenTheme.Styled(
                        $"{LopenTheme.InfoHint} Open session found. Use /resume to continue or /sessions to browse.",
                        LopenTheme.Accent));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Shows the command palette and dispatches the selected command.
    /// Returns true if the selected command requests exit.
    /// </summary>
    internal async Task<bool> ShowCommandPaletteAsync(CancellationToken cancellationToken)
    {
        if (_commandPalette is null) return false;

        CommandPaletteResult result = _commandPalette.Show();
        if (result.SelectedCommand is null) return false;

        if (result.IsArgumentCommand)
        {
            _console.MarkupLine(LopenTheme.Styled(
                $"Type: /{Markup.Escape(result.SelectedCommand)} <args>", LopenTheme.Accent));
            return false;
        }

        SlashCommandResult cmdResult = await _commandRegistry.DispatchAsync(
            $"/{result.SelectedCommand}", cancellationToken);
        return cmdResult == SlashCommandResult.ExitRequested;
    }

    /// <summary>
    /// Processes a single orchestrator turn with per-turn cancellation.
    /// First Ctrl+C during processing cancels the current turn; a second Ctrl+C exits.
    /// </summary>
    internal async Task ProcessTurnAsync(CancellationToken globalToken)
    {
        using CancellationTokenSource turnCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);

        using PosixSignalRegistration sigintRegistration = PosixSignalRegistration.Create(
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
                await _orchestrator.RunStepAsync(_orchestrator.ActiveModule ?? string.Empty, cancellationToken: turnCts.Token);
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
