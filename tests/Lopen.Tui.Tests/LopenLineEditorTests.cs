using Lopen.Core.Workflow;
using NSubstitute;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class LopenLineEditorTests
{
    [Fact]
    public void Constructor_WithAnsiConsole_DoesNotThrow()
    {
        // RadLine requires ANSI support; create a console that reports ANSI capability
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));

        LopenLineEditor editor = new(console, history);
        Assert.NotNull(editor);
    }

    [Fact]
    public void ExitRequested_DefaultsFalse()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));

        LopenLineEditor editor = new(console, history);
        Assert.False(editor.ExitRequested);
    }

    [Fact]
    public void Constructor_WithCompletion_DoesNotThrow()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        TestSlashCommandRegistry registry = new([]);
        SlashCommandCompletion completion = new(registry);

        LopenLineEditor editor = new(console, history, completion);
        Assert.NotNull(editor);
    }

    [Fact]
    public void Constructor_WithPauseController_DoesNotThrow()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        IPauseController pauseController = Substitute.For<IPauseController>();

        LopenLineEditor editor = new(console, history, pauseController: pauseController);
        Assert.NotNull(editor);
    }

    private sealed class TestSlashCommandRegistry(IReadOnlyList<SlashCommandDescriptor> commands) : ISlashCommandRegistry
    {
        public IReadOnlyList<SlashCommandDescriptor> GetCommands() => commands;
    }
}
