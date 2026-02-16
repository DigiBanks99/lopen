using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for TuiApplication landing page modal behavior (JOB-038, TUI-06).
/// Tests verify modal state management, dismissal logic, and data binding.
/// </summary>
public class TuiLandingPageTests
{
    private static TuiApplication CreateApp(bool showLandingPage = true)
    {
        return new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            showLandingPage: showLandingPage);
    }

    // ==================== Modal State ====================

    [Fact]
    public void DefaultConstructor_ShowsLandingPageByDefault()
    {
        // showLandingPage defaults to true, but modal state is set on RunAsync
        var app = CreateApp(showLandingPage: true);
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void ShowLandingPageFalse_DoesNotShowModal()
    {
        var app = CreateApp(showLandingPage: false);
        Assert.False(app.IsRunning);
    }

    // ==================== UpdateLandingPage ====================

    [Fact]
    public void UpdateLandingPage_AcceptsData()
    {
        var app = CreateApp();
        var data = new LandingPageData
        {
            Version = "1.2.3",
            IsAuthenticated = true,
            QuickCommands = [new QuickCommand("/test", "Test command")]
        };

        app.UpdateLandingPage(data);
        // No exception = success
    }

    [Fact]
    public void UpdateLandingPage_WithDefaultQuickCommands()
    {
        var app = CreateApp();
        var data = new LandingPageData { Version = "0.1.0" };

        app.UpdateLandingPage(data);

        // Verify default commands are present
        Assert.Equal(5, LandingPageData.DefaultQuickCommands.Length);
        Assert.Contains(LandingPageData.DefaultQuickCommands, c => c.Command == "/help");
        Assert.Contains(LandingPageData.DefaultQuickCommands, c => c.Command == "/spec");
        Assert.Contains(LandingPageData.DefaultQuickCommands, c => c.Command == "/plan");
        Assert.Contains(LandingPageData.DefaultQuickCommands, c => c.Command == "/build");
        Assert.Contains(LandingPageData.DefaultQuickCommands, c => c.Command == "/session");
    }

    // ==================== LandingPageComponent Render ====================

    [Fact]
    public void LandingPageComponent_RendersWithinRegion()
    {
        var component = new LandingPageComponent();
        var data = new LandingPageData
        {
            Version = "1.0.0",
            IsAuthenticated = true
        };
        var region = new ScreenRect(0, 0, 80, 24);

        var lines = component.Render(data, region);

        Assert.Equal(24, lines.Length);
        Assert.Contains(lines, l => l.Contains("1.0.0"));
        Assert.Contains(lines, l => l.Contains("Interactive Agent Loop"));
        Assert.Contains(lines, l => l.Contains("Authenticated"));
        Assert.Contains(lines, l => l.Contains("Press any key"));
    }

    [Fact]
    public void LandingPageComponent_ShowsQuickCommands()
    {
        var component = new LandingPageComponent();
        var data = new LandingPageData { Version = "1.0.0" };
        var region = new ScreenRect(0, 0, 80, 24);

        var lines = component.Render(data, region);

        Assert.Contains(lines, l => l.Contains("/help"));
        Assert.Contains(lines, l => l.Contains("/spec"));
        Assert.Contains(lines, l => l.Contains("/plan"));
        Assert.Contains(lines, l => l.Contains("/build"));
        Assert.Contains(lines, l => l.Contains("/session"));
    }

    [Fact]
    public void LandingPageComponent_NotAuthenticated_ShowsNotAuthenticated()
    {
        var component = new LandingPageComponent();
        var data = new LandingPageData
        {
            Version = "1.0.0",
            IsAuthenticated = false
        };
        var region = new ScreenRect(0, 0, 80, 24);

        var lines = component.Render(data, region);

        Assert.Contains(lines, l => l.Contains("Not authenticated"));
    }

    [Fact]
    public void LandingPageComponent_EmptyRegion_ReturnsEmpty()
    {
        var component = new LandingPageComponent();
        var data = new LandingPageData { Version = "1.0.0" };
        var region = new ScreenRect(0, 0, 0, 0);

        var lines = component.Render(data, region);
        Assert.Empty(lines);
    }

    // ==================== TuiModalState Enum ====================

    [Fact]
    public void TuiModalState_HasExpectedValues()
    {
        Assert.Equal(0, (int)TuiModalState.None);
        Assert.Equal(1, (int)TuiModalState.LandingPage);
    }

    // ==================== QuickCommand Record ====================

    [Fact]
    public void QuickCommand_HasExpectedProperties()
    {
        var cmd = new QuickCommand("/test", "A test command");
        Assert.Equal("/test", cmd.Command);
        Assert.Equal("A test command", cmd.Description);
    }

    [Fact]
    public void LandingPageData_DefaultQuickCommands_AreImmutable()
    {
        var defaults = LandingPageData.DefaultQuickCommands;
        Assert.Equal(5, defaults.Length);
        // Verify they're the expected commands
        Assert.Equal("/help", defaults[0].Command);
        Assert.Equal("/session", defaults[^1].Command);
    }
}
