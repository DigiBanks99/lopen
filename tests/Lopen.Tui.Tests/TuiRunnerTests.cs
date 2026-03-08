using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class TuiRunnerTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        (IAnsiConsole _, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(null!, editor, queue, renderer, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLineEditor()
    {
        (IAnsiConsole? console, LopenLineEditor _, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, null!, queue, renderer, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPromptQueue()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue _, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, null!, renderer, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullRenderer()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer _, SlashCommandRegistry? registry) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, queue, null!, registry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullCommandRegistry()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry _) = CreateDependencies();
        Assert.Throws<ArgumentNullException>(() => new TuiRunner(console, editor, queue, renderer, null!));
    }

    [Fact]
    public async Task RunAsync_ExitsOnCancellation()
    {
        (IAnsiConsole? console, LopenLineEditor? editor, TuiUserPromptQueue? queue, Core.IOutputRenderer? renderer, SlashCommandRegistry? registry) = CreateDependencies();
        TuiRunner runner = new(console, editor, queue, renderer, registry);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        int exitCode = await runner.RunAsync(cts.Token);
        Assert.Equal(0, exitCode);
    }

    private static (IAnsiConsole console, LopenLineEditor editor, TuiUserPromptQueue queue, Lopen.Core.IOutputRenderer renderer, SlashCommandRegistry registry) CreateDependencies()
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
        SlashCommandRegistry registry = new(console, []);
        return (console, editor, queue, renderer, registry);
    }
}
