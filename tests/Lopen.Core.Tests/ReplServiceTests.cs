using FluentAssertions;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Core.Tests;

/// <summary>
/// Mock console input for testing.
/// </summary>
public class MockConsoleInput : IConsoleInput
{
    private readonly Queue<string?> _lines;
    private readonly CancellationTokenSource _cts = new();

    public MockConsoleInput(params string?[] lines)
    {
        _lines = new Queue<string?>(lines);
    }

    public string? ReadLine()
    {
        if (_lines.Count == 0)
            return null; // Simulate EOF
        return _lines.Dequeue();
    }

    public CancellationToken CancellationToken => _cts.Token;

    public void Cancel() => _cts.Cancel();
}

public class ReplServiceTests
{
    [Fact]
    public async Task RunAsync_ExitCommand_ExitsGracefully()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var repl = new ReplService(input, output);
        var commandExecuted = false;

        var result = await repl.RunAsync(async args =>
        {
            commandExecuted = true;
            return 0;
        });

        result.Should().Be(0);
        commandExecuted.Should().BeFalse();
        console.Output.Should().Contain("Goodbye!");
    }

    [Fact]
    public async Task RunAsync_QuitCommand_ExitsGracefully()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("quit");
        var repl = new ReplService(input, output);

        var result = await repl.RunAsync(async args => 0);

        result.Should().Be(0);
        console.Output.Should().Contain("Goodbye!");
    }

    [Fact]
    public async Task RunAsync_EOF_ExitsGracefully()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput(); // Empty = EOF immediately
        var repl = new ReplService(input, output);

        var result = await repl.RunAsync(async args => 0);

        result.Should().Be(0);
        console.Output.Should().Contain("Goodbye!");
    }

    [Fact]
    public async Task RunAsync_ExecutesCommands()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("version", "exit");
        var repl = new ReplService(input, output);
        var executedArgs = new List<string[]>();

        await repl.RunAsync(async args =>
        {
            executedArgs.Add(args);
            return 0;
        });

        executedArgs.Should().HaveCount(1);
        executedArgs[0].Should().BeEquivalentTo(["version"]);
    }

    [Fact]
    public async Task RunAsync_SkipsEmptyLines()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("", "   ", "version", "exit");
        var repl = new ReplService(input, output);
        var executedArgs = new List<string[]>();

        await repl.RunAsync(async args =>
        {
            executedArgs.Add(args);
            return 0;
        });

        executedArgs.Should().HaveCount(1);
        executedArgs[0].Should().BeEquivalentTo(["version"]);
    }

    [Fact]
    public async Task RunAsync_ParsesMultipleArgs()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("version --format json", "exit");
        var repl = new ReplService(input, output);
        var executedArgs = new List<string[]>();

        await repl.RunAsync(async args =>
        {
            executedArgs.Add(args);
            return 0;
        });

        executedArgs.Should().HaveCount(1);
        executedArgs[0].Should().BeEquivalentTo(["version", "--format", "json"]);
    }

    [Fact]
    public async Task RunAsync_HandlesCommandErrors()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("badcommand", "exit");
        var repl = new ReplService(input, output);

        await repl.RunAsync(async args =>
        {
            throw new InvalidOperationException("Test error");
        });

        console.Output.Should().Contain("Command failed");
        console.Output.Should().Contain("Test error");
    }

    [Fact]
    public async Task RunAsync_DisplaysPrompt()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var repl = new ReplService(input, output, "test> ");

        await repl.RunAsync(async args => 0);

        console.Output.Should().Contain("test>");
    }

    [Fact]
    public async Task RunAsync_DisplaysStartupMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var repl = new ReplService(input, output);

        await repl.RunAsync(async args => 0);

        console.Output.Should().Contain("REPL started");
    }

    [Fact]
    public async Task RunAsync_ExitIsCaseInsensitive()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("EXIT");
        var repl = new ReplService(input, output);
        var commandExecuted = false;

        await repl.RunAsync(async args =>
        {
            commandExecuted = true;
            return 0;
        });

        commandExecuted.Should().BeFalse();
        console.Output.Should().Contain("Goodbye!");
    }

    [Fact]
    public async Task RunAsync_CancellationStopsLoop()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("version", "exit");
        var repl = new ReplService(input, output);
        using var cts = new CancellationTokenSource();
        var commandCount = 0;

        await repl.RunAsync(async args =>
        {
            commandCount++;
            cts.Cancel(); // Cancel after first command
            return 0;
        }, cts.Token);

        commandCount.Should().Be(1);
    }
}
