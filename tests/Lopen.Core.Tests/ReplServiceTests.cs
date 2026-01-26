using Shouldly;
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

        result.ShouldBe(0);
        commandExecuted.ShouldBeFalse();
        console.Output.ShouldContain("Goodbye!");
    }

    [Fact]
    public async Task RunAsync_QuitCommand_ExitsGracefully()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("quit");
        var repl = new ReplService(input, output);

        var result = await repl.RunAsync(async args => 0);

        result.ShouldBe(0);
        console.Output.ShouldContain("Goodbye!");
    }

    [Fact]
    public async Task RunAsync_EOF_ExitsGracefully()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput(); // Empty = EOF immediately
        var repl = new ReplService(input, output);

        var result = await repl.RunAsync(async args => 0);

        result.ShouldBe(0);
        console.Output.ShouldContain("Goodbye!");
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

        executedArgs.Count().ShouldBe(1);
        executedArgs[0].ShouldBe(new[] { "version" });
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

        executedArgs.Count().ShouldBe(1);
        executedArgs[0].ShouldBe(new[] { "version" });
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

        executedArgs.Count().ShouldBe(1);
        executedArgs[0].ShouldBe(new[] { "version", "--format", "json" });
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

        console.Output.ShouldContain("Command failed");
        console.Output.ShouldContain("Test error");
    }

    [Fact]
    public async Task RunAsync_DisplaysPrompt()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var repl = new ReplService(input, output, "test> ");

        await repl.RunAsync(async args => 0);

        console.Output.ShouldContain("test>");
    }

    [Fact]
    public async Task RunAsync_DisplaysStartupMessage()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var repl = new ReplService(input, output);

        await repl.RunAsync(async args => 0);

        console.Output.ShouldContain("REPL started");
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

        commandExecuted.ShouldBeFalse();
        console.Output.ShouldContain("Goodbye!");
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

        commandCount.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WithSessionState_InitializesSession()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var authService = new MockAuthService(true, "testuser", "env var");
        var sessionService = new SessionStateService(authService);
        var repl = new ReplService(input, output, sessionService);

        await repl.RunAsync(async args => 0);

        repl.SessionState.ShouldNotBeNull();
        repl.SessionState!.IsAuthenticated.ShouldBeTrue();
        repl.SessionState.Username.ShouldBe("testuser");
    }

    [Fact]
    public async Task RunAsync_WithSessionState_RecordsCommands()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("version", "help", "exit");
        var authService = new MockAuthService();
        var sessionService = new SessionStateService(authService);
        var repl = new ReplService(input, output, sessionService);

        await repl.RunAsync(async args => 0);

        repl.SessionState.ShouldNotBeNull();
        repl.SessionState!.CommandCount.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WithoutSessionState_WorksNormally()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("version", "exit");
        var repl = new ReplService(input, output);

        await repl.RunAsync(async args => 0);

        repl.SessionState.ShouldBeNull();
        console.Output.ShouldContain("Goodbye!");
    }

    [Fact]
    public async Task SessionState_ReturnsCurrentState()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var input = new MockConsoleInput("exit");
        var authService = new MockAuthService();
        var sessionService = new SessionStateService(authService);
        var repl = new ReplService(input, output, sessionService);

        // Before running
        repl.SessionState.ShouldNotBeNull();
        var sessionIdBefore = repl.SessionState!.SessionId;

        await repl.RunAsync(async args => 0);

        // After running - should have new session
        repl.SessionState!.SessionId.ShouldNotBe(sessionIdBefore);
    }
    
    // ParseArgs tests
    
    [Fact]
    public void ParseArgs_SimpleArguments()
    {
        var args = ReplService.ParseArgs("version --format json");
        
        args.ShouldBe(new[] { "version", "--format", "json" });
    }
    
    [Fact]
    public void ParseArgs_QuotedString()
    {
        var args = ReplService.ParseArgs("chat \"Hello world\"");
        
        args.ShouldBe(new[] { "chat", "Hello world" });
    }
    
    [Fact]
    public void ParseArgs_QuotedStringWithSpaces()
    {
        var args = ReplService.ParseArgs("chat \"What is 2 + 2?\" --model gpt-5");
        
        args.ShouldBe(new[] { "chat", "What is 2 + 2?", "--model", "gpt-5" });
    }
    
    [Fact]
    public void ParseArgs_MultipleQuotedStrings()
    {
        var args = ReplService.ParseArgs("echo \"first arg\" \"second arg\"");
        
        args.ShouldBe(new[] { "echo", "first arg", "second arg" });
    }
    
    [Fact]
    public void ParseArgs_EscapedQuote()
    {
        var args = ReplService.ParseArgs("chat \"He said \\\"hello\\\"\"");
        
        args.ShouldBe(new[] { "chat", "He said \"hello\"" });
    }
    
    [Fact]
    public void ParseArgs_EscapedBackslash()
    {
        var args = ReplService.ParseArgs("path \"C:\\\\Users\\\\test\"");
        
        args.ShouldBe(new[] { "path", "C:\\Users\\test" });
    }
    
    [Fact]
    public void ParseArgs_EmptyInput()
    {
        var args = ReplService.ParseArgs("");
        
        args.ShouldBeEmpty();
    }
    
    [Fact]
    public void ParseArgs_WhitespaceOnly()
    {
        var args = ReplService.ParseArgs("   ");
        
        args.ShouldBeEmpty();
    }
    
    [Fact]
    public void ParseArgs_MultipleSpaces()
    {
        var args = ReplService.ParseArgs("version   --format   json");
        
        args.ShouldBe(new[] { "version", "--format", "json" });
    }
    
    [Fact]
    public void ParseArgs_MixedQuotedAndUnquoted()
    {
        var args = ReplService.ParseArgs("cmd arg1 \"arg with space\" arg3");
        
        args.ShouldBe(new[] { "cmd", "arg1", "arg with space", "arg3" });
    }
    
    [Fact]
    public void ParseArgs_TabsAsSeparators()
    {
        var args = ReplService.ParseArgs("version\t--format\tjson");
        
        args.ShouldBe(new[] { "version", "--format", "json" });
    }
    
    [Fact]
    public void ParseArgs_QuotedAtStart()
    {
        var args = ReplService.ParseArgs("\"quoted command\" arg");
        
        args.ShouldBe(new[] { "quoted command", "arg" });
    }
}
