using Lopen.Tui.Commands;
using Spectre.Console;

namespace Lopen.Tui.Tests.Commands;

public class SlashCommandRegistryTests
{
    private static IAnsiConsole CreateTestConsole() => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(TextWriter.Null),
    });

    [Fact]
    public async Task Dispatch_RoutesToCorrectCommand()
    {
        IAnsiConsole console = CreateTestConsole();
        var command = new TestCommand("help");
        var registry = new SlashCommandRegistry(console, [command]);

        SlashCommandResult result = await registry.DispatchAsync("/help");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.True(command.WasExecuted);
    }

    [Fact]
    public async Task Dispatch_NonSlashInput_ReturnsNotACommand()
    {
        IAnsiConsole console = CreateTestConsole();
        var registry = new SlashCommandRegistry(console, []);

        SlashCommandResult result = await registry.DispatchAsync("hello world");

        Assert.Equal(SlashCommandResult.NotACommand, result);
    }

    [Fact]
    public async Task Dispatch_UnknownCommand_ReturnsHandled()
    {
        IAnsiConsole console = CreateTestConsole();
        var registry = new SlashCommandRegistry(console, []);

        SlashCommandResult result = await registry.DispatchAsync("/unknown");

        Assert.Equal(SlashCommandResult.Handled, result);
    }

    [Fact]
    public async Task Dispatch_ParsesArguments()
    {
        IAnsiConsole console = CreateTestConsole();
        var command = new TestCommand("model");
        var registry = new SlashCommandRegistry(console, [command]);

        await registry.DispatchAsync("/model gpt-4.1");

        Assert.Equal("gpt-4.1", command.LastArgs);
    }

    [Fact]
    public async Task Dispatch_CaseInsensitive()
    {
        IAnsiConsole console = CreateTestConsole();
        var command = new TestCommand("help");
        var registry = new SlashCommandRegistry(console, [command]);

        SlashCommandResult result = await registry.DispatchAsync("/HELP");

        Assert.Equal(SlashCommandResult.Handled, result);
        Assert.True(command.WasExecuted);
    }

    [Fact]
    public void GetCommands_ReturnsAllRegistered()
    {
        IAnsiConsole console = CreateTestConsole();
        var commands = new ISlashCommand[]
        {
            new TestCommand("help"),
            new TestCommand("model"),
            new TestCommand("exit"),
        };
        var registry = new SlashCommandRegistry(console, commands);

        IReadOnlyList<SlashCommandDescriptor> descriptors = registry.GetCommands();

        Assert.Equal(3, descriptors.Count);
        Assert.Contains(descriptors, d => d.Name == "help");
        Assert.Contains(descriptors, d => d.Name == "model");
        Assert.Contains(descriptors, d => d.Name == "exit");
    }

    [Fact]
    public async Task Dispatch_EmptyInput_ReturnsNotACommand()
    {
        IAnsiConsole console = CreateTestConsole();
        var registry = new SlashCommandRegistry(console, []);

        SlashCommandResult result = await registry.DispatchAsync("");

        Assert.Equal(SlashCommandResult.NotACommand, result);
    }

    [Fact]
    public async Task Dispatch_ExitCommand_ReturnsExitRequested()
    {
        IAnsiConsole console = CreateTestConsole();
        var command = new ExitReturningCommand("exit");
        var registry = new SlashCommandRegistry(console, [command]);

        SlashCommandResult result = await registry.DispatchAsync("/exit");

        Assert.Equal(SlashCommandResult.ExitRequested, result);
    }

    private sealed class TestCommand(string name) : ISlashCommand
    {
        public string Name => name;
        public string Description => $"Test {name} command";
        public bool WasExecuted { get; private set; }
        public string LastArgs { get; private set; } = "";

        public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            LastArgs = args;
            return Task.FromResult(SlashCommandResult.Handled);
        }
    }

    private sealed class ExitReturningCommand(string name) : ISlashCommand
    {
        public string Name => name;
        public string Description => "Exit";

        public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SlashCommandResult.ExitRequested);
        }
    }
}
