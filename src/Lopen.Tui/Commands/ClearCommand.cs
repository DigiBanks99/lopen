using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class ClearCommand(IAnsiConsole console) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;

    public string Name => "clear";
    public string Description => "Clear terminal";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        _console.Clear();
        return Task.FromResult(SlashCommandResult.Handled);
    }
}
