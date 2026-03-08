using Spectre.Console;

namespace Lopen.Tui.Tests;

public class LopenLineEditorTests
{
    [Fact]
    public void Constructor_WithAnsiConsole_DoesNotThrow()
    {
        // RadLine requires ANSI support; create a console that reports ANSI capability
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var history = new FileLineEditorHistory(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));

        var editor = new LopenLineEditor(console, history);
        Assert.NotNull(editor);
    }

    [Fact]
    public void Constructor_WithCompletion_DoesNotThrow()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var history = new FileLineEditorHistory(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        var registry = new TestSlashCommandRegistry([]);
        var completion = new SlashCommandCompletion(registry);

        var editor = new LopenLineEditor(console, history, completion);
        Assert.NotNull(editor);
    }

    private sealed class TestSlashCommandRegistry(IReadOnlyList<SlashCommandDescriptor> commands) : ISlashCommandRegistry
    {
        public IReadOnlyList<SlashCommandDescriptor> GetCommands() => commands;
    }
}
