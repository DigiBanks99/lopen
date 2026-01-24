using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

public class CommandAutoCompleterTests
{
    private CommandAutoCompleter CreateCompleterWithCommands()
    {
        var completer = new CommandAutoCompleter();
        completer.RegisterCommand("version", "Display version information", options: ["--format", "-f"]);
        completer.RegisterCommand("help", "Display help information", options: ["--format", "-f"]);
        completer.RegisterCommand("auth", "Authentication commands", 
            subcommands: ["login", "logout", "status"], 
            options: ["--token"]);
        completer.RegisterCommand("repl", "Start interactive REPL mode");
        return completer;
    }

    [Fact]
    public void GetCompletions_EmptyInput_ReturnsAllCommands()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("", 0);

        completions.Should().HaveCount(4);
        completions.Select(c => c.Text).Should().Contain(["version", "help", "auth", "repl"]);
    }

    [Fact]
    public void GetCompletions_PartialCommand_ReturnsMatchingCommands()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("ver", 3);

        completions.Should().HaveCount(1);
        completions[0].Text.Should().Be("version");
    }

    [Fact]
    public void GetCompletions_PartialCommand_CaseInsensitive()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("VER", 3);

        completions.Should().HaveCount(1);
        completions[0].Text.Should().Be("version");
    }

    [Fact]
    public void GetCompletions_CommandWithSpace_ReturnsSubcommandsAndOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("auth ", 5);

        completions.Should().Contain(c => c.Text == "login");
        completions.Should().Contain(c => c.Text == "logout");
        completions.Should().Contain(c => c.Text == "status");
        completions.Should().Contain(c => c.Text == "--token");
    }

    [Fact]
    public void GetCompletions_PartialSubcommand_ReturnsMatchingSubcommands()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("auth log", 8);

        completions.Should().HaveCount(2);
        completions.Select(c => c.Text).Should().Contain(["login", "logout"]);
    }

    [Fact]
    public void GetCompletions_PartialOption_ReturnsMatchingOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("version --for", 13);

        completions.Should().HaveCount(1);
        completions[0].Text.Should().Be("--format");
    }

    [Fact]
    public void GetCompletions_ShortOption_ReturnsMatchingOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("version -", 9);

        completions.Should().Contain(c => c.Text == "-f");
    }

    [Fact]
    public void GetCompletions_UnknownCommand_ReturnsEmpty()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("unknown ", 8);

        completions.Should().BeEmpty();
    }

    [Fact]
    public void GetCompletions_NoMatchingPrefix_ReturnsEmpty()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("xyz", 3);

        completions.Should().BeEmpty();
    }

    [Fact]
    public void GetCompletions_IncludesDescription()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("ver", 3);

        completions[0].Description.Should().Be("Display version information");
    }

    [Fact]
    public void RegisterCommand_AddsCommand()
    {
        var completer = new CommandAutoCompleter();

        completer.RegisterCommand("test", "Test command");

        completer.Commands.Should().HaveCount(1);
        completer.Commands[0].Name.Should().Be("test");
    }

    [Fact]
    public void RegisterCommands_AddsBulkCommands()
    {
        var completer = new CommandAutoCompleter();
        var commands = new[]
        {
            new CommandDefinition("cmd1", "Command 1"),
            new CommandDefinition("cmd2", "Command 2")
        };

        completer.RegisterCommands(commands);

        completer.Commands.Should().HaveCount(2);
    }

    [Fact]
    public void GetCompletions_AfterSubcommand_ReturnsOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("auth login --", 13);

        completions.Should().Contain(c => c.Text == "--token");
    }

    [Fact]
    public void GetCompletions_CommandWithoutSubcommands_ReturnsOnlyOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("version ", 8);

        completions.Should().Contain(c => c.Text == "--format");
        completions.Should().Contain(c => c.Text == "-f");
        completions.Should().NotContain(c => c.Text == "login");
    }

    [Fact]
    public void CompletionItem_HasTextAndDescription()
    {
        var item = new CompletionItem("test", "A test item");

        item.Text.Should().Be("test");
        item.Description.Should().Be("A test item");
    }

    [Fact]
    public void CompletionItem_DescriptionIsOptional()
    {
        var item = new CompletionItem("test");

        item.Text.Should().Be("test");
        item.Description.Should().BeNull();
    }

    [Fact]
    public void CommandDefinition_HasDefaultEmptyLists()
    {
        var cmd = new CommandDefinition("test");

        cmd.Subcommands.Should().BeEmpty();
        cmd.Options.Should().BeEmpty();
    }
}
