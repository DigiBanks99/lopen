using Lopen.Configuration;
using Lopen.Tui.Commands;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class ModelCommandTests
{
    private static IAnsiConsole CreateTestConsole() => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(TextWriter.Null),
    });

    [Fact]
    public async Task Execute_NoArgs_ReturnsHandled()
    {
        ModelCommand command = CreateCommand();
        SlashCommandResult result = await command.ExecuteAsync("");
        Assert.Equal(SlashCommandResult.Handled, result);
    }

    [Fact]
    public async Task Execute_WithModel_SwitchesAllPhases()
    {
        var options = new LopenOptions();
        ModelCommand command = CreateCommand(options);

        await command.ExecuteAsync("gpt-4.1");

        Assert.Equal("gpt-4.1", options.Models.RequirementGathering);
        Assert.Equal("gpt-4.1", options.Models.Planning);
        Assert.Equal("gpt-4.1", options.Models.Building);
        Assert.Equal("gpt-4.1", options.Models.Research);
    }

    [Fact]
    public async Task Execute_WithModel_ReturnsHandled()
    {
        ModelCommand command = CreateCommand();
        SlashCommandResult result = await command.ExecuteAsync("gpt-4.1");
        Assert.Equal(SlashCommandResult.Handled, result);
    }

    private static ModelCommand CreateCommand(LopenOptions? options = null)
    {
        options ??= new LopenOptions();
        return new ModelCommand(CreateTestConsole(), Options.Create(options));
    }
}
