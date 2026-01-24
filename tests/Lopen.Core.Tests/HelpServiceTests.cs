using FluentAssertions;
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

        result.Should().Contain("lopen - GitHub Copilot CLI");
        result.Should().Contain("Commands:");
        result.Should().Contain("version");
        result.Should().Contain("auth");
    }

    [Fact]
    public void FormatCommandListAsJson_ReturnsValidJson()
    {
        var commands = new[]
        {
            new CommandInfo("version", "Display version information")
        };

        var result = _service.FormatCommandListAsJson("lopen", "GitHub Copilot CLI", commands);

        result.Should().Contain("\"name\":\"lopen\"");
        result.Should().Contain("\"commands\"");
        result.Should().Contain("\"version\"");
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

        result.Should().Contain("auth - Authentication commands");
        result.Should().Contain("Subcommands:");
        result.Should().Contain("login");
    }

    [Fact]
    public void FormatCommandHelpAsText_WithoutSubcommands_OmitsSubcommandsSection()
    {
        var command = new CommandInfo("version", "Display version information");

        var result = _service.FormatCommandHelpAsText(command);

        result.Should().Contain("version - Display version information");
        result.Should().NotContain("Subcommands:");
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

        result.Should().Contain("\"name\":\"auth\"");
        result.Should().Contain("\"subcommands\"");
        result.Should().Contain("\"login\"");
    }
}
