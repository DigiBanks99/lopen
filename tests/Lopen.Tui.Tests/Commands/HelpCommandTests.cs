using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class HelpCommandTests
{
    [Fact]
    public async Task Execute_ReturnsHandled()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var registry = new SlashCommandRegistry(console, []);
        var command = new HelpCommand(console, new Lazy<ISlashCommandRegistry>(() => registry));

        SlashCommandResult result = await command.ExecuteAsync("");

        Assert.Equal(SlashCommandResult.Handled, result);
    }
}
