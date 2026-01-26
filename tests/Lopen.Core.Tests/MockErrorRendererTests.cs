using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class MockErrorRendererTests
{
    [Fact]
    public void RenderSimpleError_RecordsMessage()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderSimpleError("Test error");

        renderer.SimpleErrors.Count.ShouldBe(1);
        renderer.SimpleErrors[0].ShouldBe("Test error");
    }

    [Fact]
    public void RenderSimpleError_RecordsSuggestion()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderSimpleError("Error", "Try this");

        renderer.SimpleSuggestions.Count.ShouldBe(1);
        renderer.SimpleSuggestions[0].ShouldBe("Try this");
    }

    [Fact]
    public void RenderSimpleError_RecordsNullSuggestion()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderSimpleError("Error");

        renderer.SimpleSuggestions[0].ShouldBeNull();
    }

    [Fact]
    public void RenderPanelError_RecordsAllFields()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderPanelError("Title", "Message", new[] { "Suggestion1", "Suggestion2" });

        renderer.PanelErrors.Count.ShouldBe(1);
        var (title, message, suggestions) = renderer.PanelErrors[0];
        title.ShouldBe("Title");
        message.ShouldBe("Message");
        suggestions.ShouldBe(new[] { "Suggestion1", "Suggestion2" });
    }

    [Fact]
    public void RenderPanelError_HandlesNullSuggestions()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderPanelError("Title", "Message");

        renderer.PanelErrors[0].Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void RenderValidationError_RecordsAllFields()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderValidationError("--model xyz", "Invalid model", new[] { "gpt-4", "claude" });

        renderer.ValidationErrors.Count.ShouldBe(1);
        var (input, message, options) = renderer.ValidationErrors[0];
        input.ShouldBe("--model xyz");
        message.ShouldBe("Invalid model");
        options.ShouldBe(new[] { "gpt-4", "claude" });
    }

    [Fact]
    public void RenderCommandNotFound_RecordsAllFields()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderCommandNotFound("chatr", new[] { "chat", "repl" });

        renderer.CommandNotFoundErrors.Count.ShouldBe(1);
        var (command, suggestions) = renderer.CommandNotFoundErrors[0];
        command.ShouldBe("chatr");
        suggestions.ShouldBe(new[] { "chat", "repl" });
    }

    [Fact]
    public void RenderError_RecordsErrorInfo()
    {
        var renderer = new MockErrorRenderer();
        var error = new ErrorInfo
        {
            Title = "Test Error",
            Message = "Something went wrong",
            DidYouMean = "something",
            Suggestions = new[] { "Option 1", "Option 2" },
            TryCommand = "lopen help",
            Severity = ErrorSeverity.Warning
        };

        renderer.RenderError(error);

        renderer.Errors.Count.ShouldBe(1);
        renderer.Errors[0].Title.ShouldBe("Test Error");
        renderer.Errors[0].Severity.ShouldBe(ErrorSeverity.Warning);
    }

    [Fact]
    public void TotalErrorCount_SumsAllTypes()
    {
        var renderer = new MockErrorRenderer();

        renderer.RenderSimpleError("Simple");
        renderer.RenderPanelError("Panel", "Message");
        renderer.RenderValidationError("input", "message", Array.Empty<string>());
        renderer.RenderCommandNotFound("cmd", Array.Empty<string>());
        renderer.RenderError(new ErrorInfo { Title = "T", Message = "M" });

        renderer.TotalErrorCount.ShouldBe(5);
    }

    [Fact]
    public void Reset_ClearsAllRecordedErrors()
    {
        var renderer = new MockErrorRenderer();
        renderer.RenderSimpleError("Error 1");
        renderer.RenderPanelError("Title", "Message");

        renderer.Reset();

        renderer.TotalErrorCount.ShouldBe(0);
        renderer.SimpleErrors.ShouldBeEmpty();
        renderer.PanelErrors.ShouldBeEmpty();
        renderer.SimpleSuggestions.ShouldBeEmpty();
    }

    [Fact]
    public void ErrorInfo_HasDefaultValues()
    {
        var error = new ErrorInfo { Title = "Test", Message = "Msg" };

        error.DidYouMean.ShouldBeNull();
        error.Suggestions.ShouldBeEmpty();
        error.TryCommand.ShouldBeNull();
        error.Severity.ShouldBe(ErrorSeverity.Error);
    }

    [Fact]
    public void ErrorSeverity_HasExpectedValues()
    {
        ErrorSeverity.Error.ShouldBe(ErrorSeverity.Error);
        ErrorSeverity.Warning.ShouldBe(ErrorSeverity.Warning);
        ErrorSeverity.Validation.ShouldBe(ErrorSeverity.Validation);
    }
}
