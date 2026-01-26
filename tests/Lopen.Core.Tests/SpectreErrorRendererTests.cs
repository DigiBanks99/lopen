using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Core.Tests;

public class SpectreErrorRendererTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        Should.Throw<ArgumentNullException>(() => new SpectreErrorRenderer(null!));
    }

    [Fact]
    public void RenderSimpleError_ShowsMessage()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderSimpleError("Authentication failed");

        console.Output.ShouldContain("Authentication failed");
        console.Output.ShouldContain("✗");
    }

    [Fact]
    public void RenderSimpleError_ShowsSuggestion()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderSimpleError("Auth failed", "lopen auth login");

        console.Output.ShouldContain("Auth failed");
        console.Output.ShouldContain("lopen auth login");
        console.Output.ShouldContain("Try");
    }

    [Fact]
    public void RenderSimpleError_WithNoColor_ShowsPlainText()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreErrorRenderer(console);

            renderer.RenderSimpleError("Error", "suggestion");

            console.Output.ShouldContain("✗ Error");
            console.Output.ShouldContain("Try: suggestion");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void RenderPanelError_ShowsTitleAndMessage()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderPanelError("Invalid Config", "Configuration file not found");

        console.Output.ShouldContain("Invalid Config");
        console.Output.ShouldContain("Configuration file not found");
    }

    [Fact]
    public void RenderPanelError_ShowsSuggestions()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderPanelError("Error", "Message", new[] { "Option A", "Option B" });

        console.Output.ShouldContain("Suggestions");
        console.Output.ShouldContain("Option A");
        console.Output.ShouldContain("Option B");
    }

    [Fact]
    public void RenderPanelError_WithNoColor_ShowsPlainStructure()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreErrorRenderer(console);

            renderer.RenderPanelError("Title", "Message", new[] { "Suggestion" });

            console.Output.ShouldContain("--- Error: Title ---");
            console.Output.ShouldContain("Message");
            console.Output.ShouldContain("Suggestions:");
            console.Output.ShouldContain("* Suggestion");
            console.Output.ShouldContain("---");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void RenderValidationError_ShowsInputContext()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderValidationError("--model xyz", "Invalid model", new[] { "gpt-4", "claude" });

        console.Output.ShouldContain("Invalid model");
        console.Output.ShouldContain("--model xyz");
        console.Output.ShouldContain("gpt-4, claude");
    }

    [Fact]
    public void RenderValidationError_ShowsWarningSymbol()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderValidationError("input", "message", Array.Empty<string>());

        console.Output.ShouldContain("⚠");
    }

    [Fact]
    public void RenderValidationError_WithNoColor_ShowsPlainText()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreErrorRenderer(console);

            renderer.RenderValidationError("input", "Invalid value", new[] { "a", "b" });

            console.Output.ShouldContain("⚠ Invalid value");
            console.Output.ShouldContain("input");
            console.Output.ShouldContain("Valid options: a, b");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void RenderCommandNotFound_ShowsCommand()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderCommandNotFound("chatr", new[] { "chat", "repl" });

        console.Output.ShouldContain("chatr");
        console.Output.ShouldContain("not found");
    }

    [Fact]
    public void RenderCommandNotFound_ShowsSuggestions()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderCommandNotFound("vrsion", new[] { "version" });

        console.Output.ShouldContain("Did you mean");
        console.Output.ShouldContain("version");
    }

    [Fact]
    public void RenderCommandNotFound_ShowsHelpHint()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);

        renderer.RenderCommandNotFound("xyz", Array.Empty<string>());

        console.Output.ShouldContain("lopen --help");
    }

    [Fact]
    public void RenderCommandNotFound_WithNoColor_ShowsPlainStructure()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreErrorRenderer(console);

            renderer.RenderCommandNotFound("chatr", new[] { "chat" });

            console.Output.ShouldContain("--- Invalid command ---");
            console.Output.ShouldContain("Command 'chatr' not found");
            console.Output.ShouldContain("Did you mean?");
            console.Output.ShouldContain("* chat");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void RenderError_ShowsErrorInfo()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);
        var error = new ErrorInfo
        {
            Title = "Network Error",
            Message = "Connection timed out",
            DidYouMean = "check your connection",
            Suggestions = new[] { "Retry", "Check firewall" },
            TryCommand = "lopen auth status"
        };

        renderer.RenderError(error);

        console.Output.ShouldContain("Network Error");
        console.Output.ShouldContain("Connection timed out");
        console.Output.ShouldContain("Did you mean");
        console.Output.ShouldContain("check your connection");
        console.Output.ShouldContain("Retry");
        console.Output.ShouldContain("lopen auth status");
    }

    [Fact]
    public void RenderError_WithWarning_ShowsWarningSymbol()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);
        var error = new ErrorInfo
        {
            Title = "Warning",
            Message = "This may cause issues",
            Severity = ErrorSeverity.Warning
        };

        renderer.RenderError(error);

        console.Output.ShouldContain("⚠");
    }

    [Fact]
    public void RenderError_WithError_ShowsErrorSymbol()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);
        var error = new ErrorInfo
        {
            Title = "Error",
            Message = "Critical failure",
            Severity = ErrorSeverity.Error
        };

        renderer.RenderError(error);

        console.Output.ShouldContain("✗");
    }

    [Fact]
    public void RenderError_WithNoColor_ShowsPlainStructure()
    {
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            var console = new TestConsole();
            var renderer = new SpectreErrorRenderer(console);
            var error = new ErrorInfo
            {
                Title = "Test",
                Message = "Details",
                DidYouMean = "hint",
                Suggestions = new[] { "S1" },
                TryCommand = "cmd"
            };

            renderer.RenderError(error);

            console.Output.ShouldContain("--- ✗ Test ---");
            console.Output.ShouldContain("Details");
            console.Output.ShouldContain("Did you mean? hint");
            console.Output.ShouldContain("Suggestions:");
            console.Output.ShouldContain("* S1");
            console.Output.ShouldContain("Try: cmd");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    [Fact]
    public void RenderError_WithMinimalInfo_Works()
    {
        var console = new TestConsole();
        var renderer = new SpectreErrorRenderer(console);
        var error = new ErrorInfo { Title = "TestTitle", Message = "TestMessage" };

        renderer.RenderError(error);

        console.Output.ShouldContain("TestTitle");
        console.Output.ShouldContain("TestMessage");
    }
}
