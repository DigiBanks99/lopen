using Lopen.Storage;
using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class SessionsCommand : ISlashCommand
{
    private readonly IAnsiConsole _console;
    private readonly ISessionManager? _sessionManager;

    public SessionsCommand(IAnsiConsole console, ISessionManager? sessionManager = null)
    {
        _console = console;
        _sessionManager = sessionManager;
    }

    public string Name => "sessions";
    public string Description => "Browse previous sessions";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        if (_sessionManager is null)
        {
            _console.MarkupLine(LopenTheme.Styled("Session management not available.", LopenTheme.Warning));
            return SlashCommandResult.Handled;
        }

        var sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            _console.MarkupLine(LopenTheme.Styled("No sessions found.", LopenTheme.Muted));
            return SlashCommandResult.Handled;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("Session ID", LopenTheme.Accent)));

        foreach (var session in sessions)
        {
            table.AddRow(Markup.Escape(session.ToString()));
        }

        _console.Write(table);
        return SlashCommandResult.Handled;
    }
}
