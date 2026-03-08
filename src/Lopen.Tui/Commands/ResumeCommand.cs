using Lopen.Core.Workflow;
using Lopen.Storage;
using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class ResumeCommand(
    IAnsiConsole console,
    ISessionManager? sessionManager = null,
    IWorkflowOrchestrator? orchestrator = null) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;
    private readonly ISessionManager? _sessionManager = sessionManager;
    private readonly IWorkflowOrchestrator? _orchestrator = orchestrator;

    public string Name => "resume";
    public string Description => "Resume last session";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        if (_sessionManager is null)
        {
            _console.MarkupLine(LopenTheme.Styled("Session management not available.", LopenTheme.Warning));
            return SlashCommandResult.Handled;
        }

        // Determine target session: explicit arg or latest
        SessionId? targetId;
        string trimmedArgs = args.Trim();

        if (!string.IsNullOrEmpty(trimmedArgs))
        {
            targetId = SessionId.TryParse(trimmedArgs);
            if (targetId is null)
            {
                _console.MarkupLine(LopenTheme.Styled($"Invalid session ID: {Markup.Escape(trimmedArgs)}", LopenTheme.Warning));
                return SlashCommandResult.Handled;
            }
        }
        else
        {
            targetId = await _sessionManager.GetLatestSessionIdAsync(cancellationToken);
        }

        if (targetId is null)
        {
            _console.MarkupLine(LopenTheme.Styled("No incomplete sessions found.", LopenTheme.Muted));
            return SlashCommandResult.Handled;
        }

        // Load and validate session state
        SessionState? state = await _sessionManager.LoadSessionStateAsync(targetId, cancellationToken);
        if (state is null)
        {
            _console.MarkupLine(LopenTheme.Styled($"Session {Markup.Escape(targetId.ToString())} not found.", LopenTheme.Warning));
            return SlashCommandResult.Handled;
        }

        if (state.IsComplete)
        {
            _console.MarkupLine(LopenTheme.Styled($"Session {Markup.Escape(targetId.ToString())} is already complete.", LopenTheme.Warning));
            return SlashCommandResult.Handled;
        }

        // Initialize orchestrator with session resume
        if (_orchestrator is not null)
        {
            await _orchestrator.InitializeAsync(state.Module, targetId, cancellationToken);
            await _sessionManager.SetLatestAsync(targetId, cancellationToken);
            _console.MarkupLine(LopenTheme.Styled(
                $"Resuming session {Markup.Escape(targetId.ToString())} at {Markup.Escape(state.Phase)}/{Markup.Escape(state.Step)}",
                LopenTheme.Accent));
        }
        else
        {
            _console.MarkupLine(LopenTheme.Styled(
                "Cannot resume: orchestrator not available.", LopenTheme.Warning));
        }
        return SlashCommandResult.Handled;
    }
}
