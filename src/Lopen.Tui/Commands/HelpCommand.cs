using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class HelpCommand : ISlashCommand
{
    private readonly IAnsiConsole _console;
    private readonly Lazy<ISlashCommandRegistry> _registry;

    public HelpCommand(IAnsiConsole console, Lazy<ISlashCommandRegistry> registry)
    {
        _console = console;
        _registry = registry;
    }

    public string Name => "help";
    public string Description => "Show available commands";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        var commands = _registry.Value.GetCommands();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("Command", LopenTheme.Accent)).PadRight(2))
            .AddColumn(new TableColumn(LopenTheme.Styled("Description", LopenTheme.Muted)));

        foreach (var cmd in commands)
        {
            table.AddRow(
                LopenTheme.Styled($"/{cmd.Name}", LopenTheme.Accent),
                Markup.Escape(cmd.Description));
        }

        _console.Write(table);
        return Task.FromResult(SlashCommandResult.Handled);
    }
}
