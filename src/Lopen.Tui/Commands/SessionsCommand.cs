using Lopen.Storage;
using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class SessionsCommand(IAnsiConsole console, ISessionManager? sessionManager = null) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;
    private readonly ISessionManager? _sessionManager = sessionManager;

    public string Name => "sessions";
    public string Description => "Browse previous sessions";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        if (_sessionManager is null)
        {
            _console.MarkupLine(LopenTheme.Styled("Session management not available.", LopenTheme.Warning));
            return SlashCommandResult.Handled;
        }

        IReadOnlyList<SessionId> sessions = await _sessionManager.ListSessionsAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            _console.MarkupLine(LopenTheme.Styled("No sessions found.", LopenTheme.Muted));
            return SlashCommandResult.Handled;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("Session ID", LopenTheme.Accent)));

        foreach (SessionId session in sessions)
        {
            table.AddRow(Markup.Escape(session.ToString()));
        }

        _console.Write(table);
        return SlashCommandResult.Handled;
    }
}
