using Shouldly;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Core.Tests;

public class SpectreWelcomeHeaderRendererTests
{
    [Fact]
    public void RenderWelcomeHeader_WideTerminal_ShowsFullLogo()
    {
        var console = new TestConsole().Width(120);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            SessionName = "test-session",
            Terminal = new MockTerminalCapabilities { Width = 120 }
        };

        renderer.RenderWelcomeHeader(context);

        var output = console.Output;
        output.ShouldContain("Wind Runner");
        output.ShouldContain("1.0.0");
    }

    [Fact]
    public void RenderWelcomeHeader_WideTerminal_ShowsSessionInfo()
    {
        var console = new TestConsole().Width(120);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            SessionName = "my-session-123",
            ContextWindow = new ContextWindowInfo { MessageCount = 5 },
            Terminal = new MockTerminalCapabilities { Width = 120 }
        };

        renderer.RenderWelcomeHeader(context);

        var output = console.Output;
        output.ShouldContain("my-session-123");
        output.ShouldContain("5 messages");
    }

    [Fact]
    public void RenderWelcomeHeader_MediumTerminal_ShowsCompactHeader()
    {
        var console = new TestConsole().Width(70);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "2.0.0",
            SessionName = "compact-session",
            Terminal = new MockTerminalCapabilities { Width = 70 }
        };

        renderer.RenderWelcomeHeader(context);

        var output = console.Output;
        output.ShouldContain("lopen");
        output.ShouldContain("2.0.0");
        output.ShouldNotContain("Wind Runner"); // Full logo not shown
    }

    [Fact]
    public void RenderWelcomeHeader_NarrowTerminal_ShowsMinimalHeader()
    {
        var console = new TestConsole().Width(40);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "3.0.0",
            SessionName = "very-long-session-name-that-should-be-truncated",
            Terminal = new MockTerminalCapabilities { Width = 40 }
        };

        renderer.RenderWelcomeHeader(context);

        var output = console.Output;
        output.ShouldContain("lopen v3.0.0");
        output.ShouldContain("help");
    }

    [Fact]
    public void RenderWelcomeHeader_ShowsTagline()
    {
        var console = new TestConsole().Width(100);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            Terminal = new MockTerminalCapabilities { Width = 100 }
        };

        renderer.RenderWelcomeHeader(context);

        console.Output.ShouldContain("Interactive Copilot Agent Loop");
    }

    [Fact]
    public void RenderWelcomeHeader_ShowsHelpTip()
    {
        var console = new TestConsole().Width(100);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            Terminal = new MockTerminalCapabilities { Width = 100 }
        };

        renderer.RenderWelcomeHeader(context);

        console.Output.ShouldContain("help");
    }

    [Fact]
    public void RenderWelcomeHeader_WithTokenInfo_ShowsContextCapacity()
    {
        var console = new TestConsole().Width(100);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            SessionName = "token-session",
            ContextWindow = new ContextWindowInfo 
            { 
                TokensUsed = 2500, 
                TokensTotal = 128000 
            },
            Terminal = new MockTerminalCapabilities { Width = 100 }
        };

        renderer.RenderWelcomeHeader(context);

        var output = console.Output;
        output.ShouldContain("2.5K");
        output.ShouldContain("128.0K");
    }

    [Fact]
    public void RenderWelcomeHeader_LogoDisabled_HidesLogo()
    {
        var console = new TestConsole().Width(120);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            Preferences = new WelcomeHeaderPreferences { ShowLogo = false },
            Terminal = new MockTerminalCapabilities { Width = 120 }
        };

        renderer.RenderWelcomeHeader(context);

        console.Output.ShouldNotContain("Wind Runner");
    }

    [Fact]
    public void RenderWelcomeHeader_TipDisabled_HidesTip()
    {
        var console = new TestConsole().Width(100);
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            Preferences = new WelcomeHeaderPreferences { ShowTip = false, ShowLogo = false },
            Terminal = new MockTerminalCapabilities { Width = 100 }
        };

        renderer.RenderWelcomeHeader(context);

        // Tip text should not appear
        console.Output.ShouldNotContain("Type 'help'");
    }

    [Fact]
    public void RenderWelcomeHeader_UsesTerminalFromContext()
    {
        var console = new TestConsole().Width(200); // Console is wide
        var renderer = new SpectreWelcomeHeaderRenderer(console);
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0",
            Terminal = new MockTerminalCapabilities { Width = 40 } // But context says narrow
        };

        renderer.RenderWelcomeHeader(context);

        // Should use narrow layout from context terminal
        var output = console.Output;
        output.ShouldNotContain("Wind Runner");
    }
}
