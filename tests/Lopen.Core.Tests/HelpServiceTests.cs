using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class HelpServiceTests
{
    private readonly HelpService _service = new();

    [Fact]
    public void FormatCommandListAsText_ReturnsFormattedList()
    {
        var commands = new[]
        {
            new CommandInfo("version", "Display version information"),
            new CommandInfo("auth", "Authentication commands")
        };

        var result = _service.FormatCommandListAsText("lopen", "GitHub Copilot CLI", commands);

        result.ShouldContain("lopen - GitHub Copilot CLI");
        result.ShouldContain("Commands:");
        result.ShouldContain("version");
        result.ShouldContain("auth");
    }

    [Fact]
    public void FormatCommandListAsJson_ReturnsValidJson()
    {
        var commands = new[]
        {
            new CommandInfo("version", "Display version information")
        };

        var result = _service.FormatCommandListAsJson("lopen", "GitHub Copilot CLI", commands);

        result.ShouldContain("\"name\":\"lopen\"");
        result.ShouldContain("\"commands\"");
        result.ShouldContain("\"version\"");
    }

    [Fact]
    public void FormatCommandHelpAsText_WithSubcommands_ShowsSubcommands()
    {
        var command = new CommandInfo(
            "auth",
            "Authentication commands",
            new[] { new CommandInfo("login", "Login to GitHub") }
        );

        var result = _service.FormatCommandHelpAsText(command);

        result.ShouldContain("auth - Authentication commands");
        result.ShouldContain("Subcommands:");
        result.ShouldContain("login");
    }

    [Fact]
    public void FormatCommandHelpAsText_WithoutSubcommands_OmitsSubcommandsSection()
    {
        var command = new CommandInfo("version", "Display version information");

        var result = _service.FormatCommandHelpAsText(command);

        result.ShouldContain("version - Display version information");
        result.ShouldNotContain("Subcommands:");
    }

    [Fact]
    public void FormatCommandHelpAsJson_ReturnsValidJson()
    {
        var command = new CommandInfo(
            "auth",
            "Authentication commands",
            new[] { new CommandInfo("login", "Login to GitHub") }
        );

        var result = _service.FormatCommandHelpAsJson(command);

        result.ShouldContain("\"name\":\"auth\"");
        result.ShouldContain("\"subcommands\"");
        result.ShouldContain("\"login\"");
    }
}
