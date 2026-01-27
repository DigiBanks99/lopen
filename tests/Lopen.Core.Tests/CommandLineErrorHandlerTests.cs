using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class CommandLineErrorHandlerTests
{
    private readonly MockErrorRenderer _mockRenderer;
    private readonly List<string> _availableCommands;

    public CommandLineErrorHandlerTests()
    {
        _mockRenderer = new MockErrorRenderer();
        _availableCommands = new List<string> { "auth", "chat", "help", "loop", "repl", "sessions", "test", "version" };
    }

    [Fact]
    public void Constructor_ThrowsOnNullErrorRenderer()
    {
        Should.Throw<ArgumentNullException>(() => new CommandLineErrorHandler(null!, _availableCommands));
    }

    [Fact]
    public void Constructor_AcceptsNullAvailableCommands()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, null!);
        handler.ShouldNotBeNull();
    }

    [Fact]
    public void HandleParseErrors_WithNoErrors_ReturnsZero()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);

        var result = handler.HandleParseErrors(Enumerable.Empty<ParseErrorInfo>());

        result.ShouldBe(0);
        _mockRenderer.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void HandleParseErrors_WithError_ReturnsInvalidArgumentsExitCode()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Some error") };

        var result = handler.HandleParseErrors(errors);

        result.ShouldBe(ExitCodes.InvalidArguments);
    }

    [Fact]
    public void HandleParseErrors_WithUnknownCommand_RendersInvalidCommand()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'chatr'") };

        handler.HandleParseErrors(errors);

        _mockRenderer.Errors.ShouldHaveSingleItem();
        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Title.ShouldBe("Invalid command");
        errorInfo.Message.ShouldContain("chatr");
        errorInfo.Message.ShouldContain("not found");
    }

    [Fact]
    public void HandleParseErrors_WithUnknownCommand_SuggestsSimilarCommands()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'chatr'") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Suggestions.ShouldContain("chat");
    }

    [Fact]
    public void HandleParseErrors_WithUnknownCommand_SuggestsHelpCommand()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'xyz'") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.TryCommand.ShouldBe("lopen --help");
    }

    [Fact]
    public void HandleParseErrors_WithRequiredArgumentMissing_RendersCorrectError()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Required argument missing for command: 'chat'") };

        handler.HandleParseErrors(errors);

        _mockRenderer.Errors.ShouldHaveSingleItem();
        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Title.ShouldBe("Missing argument");
    }

    [Fact]
    public void HandleParseErrors_WithUnrecognizedOption_RendersInvalidOption()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument '--badoption'") };

        handler.HandleParseErrors(errors);

        _mockRenderer.Errors.ShouldHaveSingleItem();
        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Title.ShouldBe("Invalid option");
    }

    [Fact]
    public void HandleParseErrors_WithGenericError_RendersCommandError()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Something unexpected happened") };

        handler.HandleParseErrors(errors);

        _mockRenderer.Errors.ShouldHaveSingleItem();
        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Title.ShouldBe("Command Error");
    }

    [Fact]
    public void HandleParseErrors_WithMultipleErrors_RendersAll()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[]
        {
            new ParseErrorInfo("Error 1"),
            new ParseErrorInfo("Error 2")
        };

        handler.HandleParseErrors(errors);

        _mockRenderer.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void HandleParseErrors_WithCommandContext_IncludesContextInHelpCommand()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Option '--invalid' is required.") };
        var tokens = new[] { "chat" };

        handler.HandleParseErrors(errors, tokens);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.TryCommand.ShouldBe("lopen chat --help");
    }

    [Fact]
    public void HandleParseErrors_WithSubcommandContext_IncludesFullContextInHelpCommand()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Option '--invalid' is required.") };
        var tokens = new[] { "test", "self" };

        handler.HandleParseErrors(errors, tokens);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.TryCommand.ShouldBe("lopen test self --help");
    }

    [Fact]
    public void HandleParseErrors_SuggestionsSortedByDistance()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        // "lop" is closer to "loop" than "help"
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'lop'") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Suggestions.ShouldNotBeEmpty();
        errorInfo.Suggestions[0].ShouldBe("loop");
    }

    [Fact]
    public void HandleParseErrors_LimitsToThreeSuggestions()
    {
        // With many similar commands, only 3 should be suggested
        var manyCommands = new List<string> { "aaa", "aab", "aac", "aad", "aae" };
        var handler = new CommandLineErrorHandler(_mockRenderer, manyCommands);
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'aa'") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Suggestions.Count.ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public void HandleParseErrors_NoSuggestionsForDistantCommand()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        // "xyz123" is very different from all commands
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'xyzabcdefgh'") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void HandleParseErrors_ValidationSeverityForKnownErrorTypes()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Unrecognized command or argument 'chatr'") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Severity.ShouldBe(ErrorSeverity.Validation);
    }

    [Fact]
    public void HandleParseErrors_ErrorSeverityForGenericErrors()
    {
        var handler = new CommandLineErrorHandler(_mockRenderer, _availableCommands);
        var errors = new[] { new ParseErrorInfo("Unknown internal error") };

        handler.HandleParseErrors(errors);

        var errorInfo = _mockRenderer.Errors[0];
        errorInfo.Severity.ShouldBe(ErrorSeverity.Error);
    }
}
