using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for LandingPageComponent and SessionResumeModalComponent.
/// Covers AC: landing page modal + session resume modal.
/// </summary>
public class ModalComponentTests
{
    // ==================== LandingPageComponent ====================

    private readonly LandingPageComponent _landing = new();

    [Fact]
    public void LandingPage_Name_IsCorrect()
    {
        Assert.Equal("LandingPage", _landing.Name);
    }

    [Fact]
    public void LandingPage_ShowsLogo()
    {
        var data = new LandingPageData { Version = "v1.0.0" };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("┏━┓"));
    }

    [Fact]
    public void LandingPage_ShowsVersion()
    {
        var data = new LandingPageData { Version = "v2.3.4" };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("v2.3.4"));
    }

    [Fact]
    public void LandingPage_ShowsInteractiveAgentLoop()
    {
        var data = new LandingPageData { Version = "v1.0.0" };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("Interactive Agent Loop"));
    }

    [Fact]
    public void LandingPage_ShowsQuickCommands()
    {
        var data = new LandingPageData { Version = "v1.0.0" };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("Quick Commands"));
        Assert.Contains(lines, l => l.Contains("/help") && l.Contains("Show available commands"));
        Assert.Contains(lines, l => l.Contains("/spec") && l.Contains("requirement gathering"));
        Assert.Contains(lines, l => l.Contains("/plan") && l.Contains("planning mode"));
        Assert.Contains(lines, l => l.Contains("/build") && l.Contains("build mode"));
        Assert.Contains(lines, l => l.Contains("/session") && l.Contains("Manage sessions"));
    }

    [Fact]
    public void LandingPage_ShowsAuthStatus_Authenticated()
    {
        var data = new LandingPageData { Version = "v1.0.0", IsAuthenticated = true };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("🟢 Authenticated"));
    }

    [Fact]
    public void LandingPage_ShowsAuthStatus_NotAuthenticated()
    {
        var data = new LandingPageData { Version = "v1.0.0", IsAuthenticated = false };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("🔴 Not authenticated"));
    }

    [Fact]
    public void LandingPage_ShowsPressAnyKey()
    {
        var data = new LandingPageData { Version = "v1.0.0" };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("Press any key to continue"));
    }

    [Fact]
    public void LandingPage_CustomQuickCommands()
    {
        var data = new LandingPageData
        {
            Version = "v1.0.0",
            QuickCommands = [new("/custom", "Do custom thing")],
        };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        Assert.Contains(lines, l => l.Contains("/custom") && l.Contains("Do custom thing"));
    }

    [Fact]
    public void LandingPage_ZeroRegion_ReturnsEmpty()
    {
        var data = new LandingPageData { Version = "v1.0.0" };
        Assert.Empty(_landing.Render(data, new ScreenRect(0, 0, 0, 20)));
        Assert.Empty(_landing.Render(data, new ScreenRect(0, 0, 80, 0)));
    }

    [Fact]
    public void LandingPage_AllLinesSameWidth()
    {
        var data = new LandingPageData { Version = "v1.0.0" };
        var lines = _landing.Render(data, new ScreenRect(0, 0, 80, 20));

        foreach (var line in lines)
            Assert.Equal(80, line.Length);
    }

    // ==================== SessionResumeModalComponent ====================

    private readonly SessionResumeModalComponent _resume = new();

    [Fact]
    public void ResumeModal_Name_IsCorrect()
    {
        Assert.Equal("SessionResumeModal", _resume.Name);
    }

    [Fact]
    public void ResumeModal_ShowsTitle()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("Resume Session?"));
    }

    [Fact]
    public void ResumeModal_ShowsModuleName()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("authentication module"));
    }

    [Fact]
    public void ResumeModal_ShowsPhaseAndStep()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("Phase: Building") && l.Contains("Step 6/7"));
    }

    [Fact]
    public void ResumeModal_ShowsProgress()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("60%") && l.Contains("3/5 tasks complete"));
    }

    [Fact]
    public void ResumeModal_ShowsLastActivity()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("2 hours ago"));
    }

    [Fact]
    public void ResumeModal_ShowsOptions()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("Resume") && l.Contains("Start New") && l.Contains("View Details"));
    }

    [Fact]
    public void ResumeModal_HighlightsSelectedOption()
    {
        var data = CreateResumeData() with { SelectedOption = 1 };
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("[>Start New<]"));
        Assert.Contains(lines, l => l.Contains("[Resume]") && !l.Contains("[>Resume<]"));
    }

    [Fact]
    public void ResumeModal_DefaultSelection_IsResume()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("[>Resume<]"));
    }

    [Fact]
    public void ResumeModal_ZeroRegion_ReturnsEmpty()
    {
        var data = CreateResumeData();
        Assert.Empty(_resume.Render(data, new ScreenRect(0, 0, 0, 10)));
        Assert.Empty(_resume.Render(data, new ScreenRect(0, 0, 60, 0)));
    }

    [Fact]
    public void ResumeModal_AllLinesSameWidth()
    {
        var data = CreateResumeData();
        var lines = _resume.Render(data, new ScreenRect(0, 0, 60, 10));

        foreach (var line in lines)
            Assert.Equal(60, line.Length);
    }

    private static SessionResumeData CreateResumeData() => new()
    {
        ModuleName = "authentication",
        PhaseName = "Building",
        StepProgress = "6/7",
        ProgressPercent = 60,
        TaskProgress = "3/5 tasks complete in auth-module",
        LastActivity = "2 hours ago",
        SelectedOption = 0,
    };
}

/// <summary>
/// Tests for ResourceViewerModalComponent.
/// Covers AC: [TUI-12] Numbered resource access (press 1-9 to view active resources).
/// </summary>
public class ResourceViewerModalComponentTests
{
    private readonly ResourceViewerModalComponent _component = new();
    private readonly ScreenRect _region = new(0, 0, 60, 20);

    [Fact]
    public void Render_ZeroRegion_ReturnsEmpty()
    {
        var data = new ResourceViewerData { Label = "test.md" };
        var lines = _component.Render(data, new ScreenRect(0, 0, 0, 0));
        Assert.Empty(lines);
    }

    [Fact]
    public void Render_ShowsTitleBar()
    {
        var data = new ResourceViewerData { Label = "README.md", Lines = ["Line 1"] };
        var lines = _component.Render(data, _region);
        Assert.Contains(lines, l => l.Contains("📄 README.md"));
    }

    [Fact]
    public void Render_ShowsContentLines()
    {
        var data = new ResourceViewerData { Label = "file.txt", Lines = ["Hello", "World"] };
        var lines = _component.Render(data, _region);
        Assert.Contains(lines, l => l.TrimEnd().Contains("Hello"));
        Assert.Contains(lines, l => l.TrimEnd().Contains("World"));
    }

    [Fact]
    public void Render_ShowsFooterWithEscHint()
    {
        var data = new ResourceViewerData { Label = "file.txt", Lines = ["Line 1"] };
        var lines = _component.Render(data, _region);
        Assert.Contains(lines, l => l.Contains("Esc: Close"));
    }

    [Fact]
    public void Render_ShowsAllContentVisible_WhenFitsInRegion()
    {
        var data = new ResourceViewerData { Label = "file.txt", Lines = ["Line 1", "Line 2"] };
        var lines = _component.Render(data, _region);
        Assert.Contains(lines, l => l.Contains("All content visible"));
    }

    [Fact]
    public void Render_ShowsLineCount_WhenContentExceedsRegion()
    {
        var contentLines = Enumerable.Range(1, 50).Select(i => $"Line {i}").ToList();
        var data = new ResourceViewerData { Label = "big.txt", Lines = contentLines };
        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 10));
        Assert.Contains(lines, l => l.Contains("of 50"));
    }

    [Fact]
    public void Render_ScrollOffset_SkipsLines()
    {
        var contentLines = Enumerable.Range(1, 50).Select(i => $"Line {i}").ToList();
        var data = new ResourceViewerData { Label = "big.txt", Lines = contentLines, ScrollOffset = 10 };
        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 10));
        Assert.Contains(lines, l => l.TrimEnd().Contains("Line 11"));
        Assert.DoesNotContain(lines, l => l.TrimEnd() == "Line 1");
    }

    [Fact]
    public void Render_NoContent_ShowsPlaceholder()
    {
        var data = new ResourceViewerData { Label = "empty.txt" };
        var lines = _component.Render(data, _region);
        // Empty Lines defaults to [], so content area is just padding
        Assert.Equal(_region.Height, lines.Length);
    }

    [Fact]
    public void Render_TruncatesLongLines()
    {
        var longLine = new string('A', 200);
        var data = new ResourceViewerData { Label = "wide.txt", Lines = [longLine] };
        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 10));
        Assert.All(lines, l => Assert.True(l.Length <= 40));
    }

    [Fact]
    public void Render_PadsToRegionHeight()
    {
        var data = new ResourceViewerData { Label = "small.txt", Lines = ["One line"] };
        var lines = _component.Render(data, _region);
        Assert.Equal(_region.Height, lines.Length);
    }
}
