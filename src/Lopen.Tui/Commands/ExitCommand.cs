using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class ExitCommand : ISlashCommand
{
    private readonly IAnsiConsole _console;

    public ExitCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public string Name => "exit";
    public string Description => "Exit lopen";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        _console.MarkupLine(LopenTheme.Styled("Goodbye!", LopenTheme.Muted));
        return Task.FromResult(SlashCommandResult.ExitRequested);
    }
}
