using NSubstitute;
using RadLine;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class OpenCommandPaletteCommandTests
{
    [Fact]
    public void CommandPaletteRequestedFlag_DefaultsToFalse()
    {
        LopenLineEditor.CommandPaletteRequestedFlag flag = new();
        Assert.False(flag.IsSet);
    }

    [Fact]
    public void CommandPaletteRequestedFlag_CanBeSetToTrue()
    {
        LopenLineEditor.CommandPaletteRequestedFlag flag = new();
        flag.IsSet = true;
        Assert.True(flag.IsSet);
    }

    [Fact]
    public void CommandPaletteRequestedFlag_CanBeReset()
    {
        LopenLineEditor.CommandPaletteRequestedFlag flag = new();
        flag.IsSet = true;
        flag.IsSet = false;
        Assert.False(flag.IsSet);
    }

    [Fact]
    public void Execute_SetsFlagToTrue()
    {
        LopenLineEditor.CommandPaletteRequestedFlag flag = new();
        OpenCommandPaletteCommand command = new(flag);

        LineBuffer buffer = new();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        LineEditorContext context = new(buffer, provider);

        command.Execute(context);

        Assert.True(flag.IsSet);
    }

    [Fact]
    public void Execute_SetsFlagRegardlessOfBufferContent()
    {
        LopenLineEditor.CommandPaletteRequestedFlag flag = new();
        OpenCommandPaletteCommand command = new(flag);

        LineBuffer buffer = new();
        buffer.Insert('h');
        buffer.Insert('e');
        buffer.Insert('l');
        buffer.Insert('l');
        buffer.Insert('o');
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        LineEditorContext context = new(buffer, provider);

        command.Execute(context);

        Assert.True(flag.IsSet);
    }

    [Fact]
    public void LopenLineEditor_CommandPaletteRequested_DefaultsFalse()
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

        Assert.False(editor.CommandPaletteRequested);
    }

    [Fact]
    public void LopenLineEditor_ResetCommandPaletteRequested_ResetsFlag()
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

        // ResetCommandPaletteRequested should be safe to call even when not set
        editor.ResetCommandPaletteRequested();
        Assert.False(editor.CommandPaletteRequested);
    }
}
