using Shouldly;
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

        completions.Count().ShouldBe(4);
        completions.Select(c => c.Text).ShouldBe(new[] { "version", "help", "auth", "repl" }, ignoreOrder: true);
    }

    [Fact]
    public void GetCompletions_PartialCommand_ReturnsMatchingCommands()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("ver", 3);

        completions.Count().ShouldBe(1);
        completions[0].Text.ShouldBe("version");
    }

    [Fact]
    public void GetCompletions_PartialCommand_CaseInsensitive()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("VER", 3);

        completions.Count().ShouldBe(1);
        completions[0].Text.ShouldBe("version");
    }

    [Fact]
    public void GetCompletions_CommandWithSpace_ReturnsSubcommandsAndOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("auth ", 5);

        completions.ShouldContain(c => c.Text == "login");
        completions.ShouldContain(c => c.Text == "logout");
        completions.ShouldContain(c => c.Text == "status");
        completions.ShouldContain(c => c.Text == "--token");
    }

    [Fact]
    public void GetCompletions_PartialSubcommand_ReturnsMatchingSubcommands()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("auth log", 8);

        completions.Count().ShouldBe(2);
        completions.Select(c => c.Text).ShouldBe(new[] { "login", "logout" }, ignoreOrder: true);
    }

    [Fact]
    public void GetCompletions_PartialOption_ReturnsMatchingOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("version --for", 13);

        completions.Count().ShouldBe(1);
        completions[0].Text.ShouldBe("--format");
    }

    [Fact]
    public void GetCompletions_ShortOption_ReturnsMatchingOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("version -", 9);

        completions.ShouldContain(c => c.Text == "-f");
    }

    [Fact]
    public void GetCompletions_UnknownCommand_ReturnsEmpty()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("unknown ", 8);

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_NoMatchingPrefix_ReturnsEmpty()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("xyz", 3);

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_IncludesDescription()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("ver", 3);

        completions[0].Description.ShouldBe("Display version information");
    }

    [Fact]
    public void RegisterCommand_AddsCommand()
    {
        var completer = new CommandAutoCompleter();

        completer.RegisterCommand("test", "Test command");

        completer.Commands.Count().ShouldBe(1);
        completer.Commands[0].Name.ShouldBe("test");
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

        completer.Commands.Count().ShouldBe(2);
    }

    [Fact]
    public void GetCompletions_AfterSubcommand_ReturnsOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("auth login --", 13);

        completions.ShouldContain(c => c.Text == "--token");
    }

    [Fact]
    public void GetCompletions_CommandWithoutSubcommands_ReturnsOnlyOptions()
    {
        var completer = CreateCompleterWithCommands();

        var completions = completer.GetCompletions("version ", 8);

        completions.ShouldContain(c => c.Text == "--format");
        completions.ShouldContain(c => c.Text == "-f");
        completions.ShouldNotContain(c => c.Text == "login");
    }

    [Fact]
    public void CompletionItem_HasTextAndDescription()
    {
        var item = new CompletionItem("test", "A test item");

        item.Text.ShouldBe("test");
        item.Description.ShouldBe("A test item");
    }

    [Fact]
    public void CompletionItem_DescriptionIsOptional()
    {
        var item = new CompletionItem("test");

        item.Text.ShouldBe("test");
        item.Description.ShouldBeNull();
    }

    [Fact]
    public void CommandDefinition_HasDefaultEmptyLists()
    {
        var cmd = new CommandDefinition("test");

        cmd.Subcommands.ShouldBeEmpty();
        cmd.Options.ShouldBeEmpty();
    }
}
