using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class ExitCommandTests
{
    [Fact]
    public async Task Execute_ReturnsExitRequested()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var command = new ExitCommand(console);

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.ExitRequested, result);
    }
}
