using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class HelpCommand(IAnsiConsole console, Lazy<ISlashCommandRegistry> registry) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;
    private readonly Lazy<ISlashCommandRegistry> _registry = registry;

    public string Name => "help";
    public string Description => "Show available commands";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SlashCommandDescriptor> commands = _registry.Value.GetCommands();

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(LopenTheme.Muted)
            .AddColumn(new TableColumn(LopenTheme.Bold("Command", LopenTheme.Accent)).PadRight(2))
            .AddColumn(new TableColumn(LopenTheme.Styled("Description", LopenTheme.Muted)));

        foreach (SlashCommandDescriptor cmd in commands)
        {
            table.AddRow(
                LopenTheme.Styled($"/{cmd.Name}", LopenTheme.Accent),
                Markup.Escape(cmd.Description));
        }

        _console.Write(table);
        return Task.FromResult(SlashCommandResult.Handled);
    }
}
