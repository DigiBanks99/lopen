using NSubstitute;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class CommandPaletteTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        ISlashCommandRegistry registry = Substitute.For<ISlashCommandRegistry>();
        Assert.Throws<ArgumentNullException>(() => new CommandPalette(null!, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRegistry()
    {
        IAnsiConsole console = CreateConsole();
        Assert.Throws<ArgumentNullException>(() => new CommandPalette(console, null!));
    }

    [Fact]
    public void Show_WithEmptyRegistry_ReturnsNullSelectedCommand()
    {
        IAnsiConsole console = CreateConsole();
        ISlashCommandRegistry registry = Substitute.For<ISlashCommandRegistry>();
        registry.GetCommands().Returns(Array.Empty<SlashCommandDescriptor>());

        CommandPalette palette = new(console, registry);

        CommandPaletteResult result = palette.Show();

        Assert.Null(result.SelectedCommand);
        Assert.False(result.IsArgumentCommand);
    }

    [Fact]
    public void Show_LoadsCommandsFromRegistry()
    {
        IAnsiConsole console = CreateConsole();
        ISlashCommandRegistry registry = Substitute.For<ISlashCommandRegistry>();
        registry.GetCommands().Returns(Array.Empty<SlashCommandDescriptor>());

        CommandPalette palette = new(console, registry);

        palette.Show();

        registry.Received(1).GetCommands();
    }

    [Fact]
    public void CommandPaletteResult_WithCommand_HasCorrectProperties()
    {
        CommandPaletteResult result = new("help", false);
        Assert.Equal("help", result.SelectedCommand);
        Assert.False(result.IsArgumentCommand);
    }

    [Fact]
    public void CommandPaletteResult_NullCommand_IsValid()
    {
        CommandPaletteResult result = new(null, false);
        Assert.Null(result.SelectedCommand);
        Assert.False(result.IsArgumentCommand);
    }

    [Fact]
    public void CommandPaletteResult_ArgumentCommand_HasIsArgumentTrue()
    {
        CommandPaletteResult result = new("model", true);
        Assert.Equal("model", result.SelectedCommand);
        Assert.True(result.IsArgumentCommand);
    }

    [Fact]
    public void CommandPaletteResult_Equality_WorksForSameValues()
    {
        CommandPaletteResult a = new("help", false);
        CommandPaletteResult b = new("help", false);
        Assert.Equal(a, b);
    }

    [Fact]
    public void CommandPaletteResult_Equality_DiffersForDifferentValues()
    {
        CommandPaletteResult a = new("help", false);
        CommandPaletteResult b = new("model", true);
        Assert.NotEqual(a, b);
    }

    private static IAnsiConsole CreateConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
}
