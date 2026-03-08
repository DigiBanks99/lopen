namespace Lopen.Tui.Tests;

public class SlashCommandCompletionTests
{
    private static SlashCommandCompletion CreateCompletion(params (string Name, string Desc)[] commands)
    {
        var registry = new TestSlashCommandRegistry(
            commands.Select(c => new SlashCommandDescriptor(c.Name, c.Desc)).ToList());
        return new SlashCommandCompletion(registry);
    }

    [Fact]
    public void SlashH_SuggestsHelp()
    {
        var completion = CreateCompletion(
            ("help", "Show help"),
            ("model", "Switch model"));

        var results = completion.GetCompletions("/", "h", "");
        Assert.NotNull(results);
        Assert.Contains("/help", results!);
    }

    [Fact]
    public void SlashM_SuggestsModel()
    {
        var completion = CreateCompletion(
            ("help", "Show help"),
            ("model", "Switch model"));

        var results = completion.GetCompletions("/", "m", "");
        Assert.NotNull(results);
        Assert.Contains("/model", results!);
    }

    [Fact]
    public void NonSlashInput_ReturnsNull()
    {
        var completion = CreateCompletion(
            ("help", "Show help"));

        var results = completion.GetCompletions("", "hello", "");
        Assert.Null(results);
    }

    [Fact]
    public void SlashAlone_SuggestsAllCommands()
    {
        var completion = CreateCompletion(
            ("help", "Show help"),
            ("model", "Switch model"),
            ("exit", "Exit lopen"));

        var results = completion.GetCompletions("", "/", "");
        Assert.NotNull(results);
        Assert.Equal(3, results!.Count());
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        var completion = CreateCompletion(
            ("help", "Show help"));

        var results = completion.GetCompletions("/", "xyz", "");
        Assert.Null(results);
    }

    [Fact]
    public void Constructor_ThrowsOnNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() => new SlashCommandCompletion(null!));
    }

    private sealed class TestSlashCommandRegistry : ISlashCommandRegistry
    {
        private readonly IReadOnlyList<SlashCommandDescriptor> _commands;

        public TestSlashCommandRegistry(IReadOnlyList<SlashCommandDescriptor> commands)
        {
            _commands = commands;
        }

        public IReadOnlyList<SlashCommandDescriptor> GetCommands() => _commands;
    }
}
