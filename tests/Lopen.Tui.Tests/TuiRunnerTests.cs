using Spectre.Console;

namespace Lopen.Tui.Tests;

public class TuiRunnerTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        (IAnsiConsole _, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(null!, editor, queue, renderer));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLineEditor()
    {
        (IAnsiConsole? console, LopenLineEditor _, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, null!, queue, renderer));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPromptQueue()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue _, Core.IOutputRenderer? renderer) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, null!, renderer));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRenderer()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer _) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, queue, null!));
    }

    [Fact]
    public async Task RunAsync_ExitsOnCancellation()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer) = CreateDependencies();
        TuiRunner runner = new(console, editor, queue, renderer);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        int exitCode = await runner.RunAsync(cts.Token);
        Assert.Equal(0, exitCode);
    }

    private static (IAnsiConsole console, LopenLineEditor editor, TuiUserPromptQueue queue, Lopen.Core.IOutputRenderer renderer) CreateDependencies()
    {
        // RadLine requires ANSI support
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        FileLineEditorHistory history = new(
            Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}", "history.txt"));
        LopenLineEditor editor = new(console, history);
        TuiUserPromptQueue queue = new();
        TuiOutputRenderer renderer = new(console, editor);
        return (console, editor, queue, renderer);
    }
}
