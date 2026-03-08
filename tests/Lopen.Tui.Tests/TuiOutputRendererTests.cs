using Spectre.Console;

namespace Lopen.Tui.Tests;

public class TuiOutputRendererTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
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

        Assert.Throws<ArgumentNullException>(() => new TuiOutputRenderer(null!, editor));
    }

    [Fact]
    public void Constructor_ThrowsOnNullEditor()
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });

        Assert.Throws<ArgumentNullException>(() => new TuiOutputRenderer(console, null!));
    }

    [Fact]
    public async Task RenderProgressAsync_DoesNotThrow()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        await renderer.RenderProgressAsync("Build", "compiling", 0.5);
    }

    [Fact]
    public async Task RenderErrorAsync_DoesNotThrow()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        await renderer.RenderErrorAsync("Something went wrong", new InvalidOperationException("test"));
    }

    [Fact]
    public async Task RenderResultAsync_DoesNotThrow()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        await renderer.RenderResultAsync("Operation complete");
    }

    [Fact]
    public void ImplementsIOutputRenderer()
    {
        (TuiOutputRenderer? renderer, IAnsiConsole _) = CreateRenderer();
        Assert.IsAssignableFrom<Lopen.Core.IOutputRenderer>(renderer);
    }

    private static (TuiOutputRenderer renderer, IAnsiConsole console) CreateRenderer()
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
        return (new TuiOutputRenderer(console, editor), console);
    }
}
