using Lopen.Storage;
using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class SessionsCommand(IAnsiConsole console, ISessionManager? sessionManager = null) : ISlashCommand
{
    private const string CancelOption = "(Cancel)";

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

        DateTimeOffset now = DateTimeOffset.UtcNow;

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("ID", LopenTheme.Accent)))
            .AddColumn(new TableColumn(LopenTheme.Bold("Module", LopenTheme.Accent)))
            .AddColumn(new TableColumn(LopenTheme.Bold("Step", LopenTheme.Accent)))
            .AddColumn(new TableColumn(LopenTheme.Bold("Updated", LopenTheme.Accent)).RightAligned());

        foreach (SessionId session in sessions)
        {
            SessionState? state = await _sessionManager.LoadSessionStateAsync(session, cancellationToken);

            string id = Markup.Escape(session.ToString());
            string module = Markup.Escape(state?.Module ?? "?");
            string step = Markup.Escape(state?.Step ?? "?");
            string updated = state is not null
                ? RelativeTimeFormatter.FormatRelativeTime(state.UpdatedAt, now)
                : "?";

            table.AddRow(id, module, step, updated);
        }

        _console.Write(table);

        if (_console.Profile.Capabilities.Interactive)
        {
            var choices = new List<string>();
            foreach (SessionId s in sessions)
            {
                choices.Add(s.ToString());
            }

            choices.Add(CancelOption);

            string selected = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title(LopenTheme.Bold("Select a session", LopenTheme.Accent))
                    .HighlightStyle(LopenTheme.AccentStyle)
                    .AddChoices(choices));

            if (selected != CancelOption)
            {
                SessionId? selectedId = SessionId.TryParse(selected);
                if (selectedId is not null)
                {
                    await _sessionManager.SetLatestAsync(selectedId, cancellationToken);
                    _console.MarkupLine(LopenTheme.Styled($"Switched to session {selected}.", LopenTheme.Success));
                }
            }
        }

        return SlashCommandResult.Handled;
    }
}
