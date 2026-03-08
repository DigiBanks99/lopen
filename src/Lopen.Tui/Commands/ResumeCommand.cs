using Lopen.Storage;
using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class ResumeCommand(IAnsiConsole console, ISessionManager? sessionManager = null) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;
    private readonly ISessionManager? _sessionManager = sessionManager;

    public string Name => "resume";
    public string Description => "Resume last session";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        if (_sessionManager is null)
        {
            _console.MarkupLine(LopenTheme.Styled("Session management not available.", LopenTheme.Warning));
            return SlashCommandResult.Handled;
        }

        SessionId? latestId = await _sessionManager.GetLatestSessionIdAsync(cancellationToken);
        if (latestId is null)
        {
            _console.MarkupLine(LopenTheme.Styled("No incomplete sessions found.", LopenTheme.Muted));
            return SlashCommandResult.Handled;
        }

        _console.MarkupLine(LopenTheme.Styled($"Resuming session {Markup.Escape(latestId.ToString())}...", LopenTheme.Accent));
        return SlashCommandResult.Handled;
    }
}
