using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class SkillsCommand(IAnsiConsole console) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;

    public string Name => "skills";
    public string Description => "List available skills";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        _console.MarkupLine(LopenTheme.Styled("No skills configured.", LopenTheme.Muted));
        return Task.FromResult(SlashCommandResult.Handled);
    }
}
