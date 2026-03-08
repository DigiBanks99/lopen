using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class ClearCommandTests
{
    [Fact]
    public async Task Execute_ReturnsHandled()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var command = new ClearCommand(console);

        var result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
    }
}
