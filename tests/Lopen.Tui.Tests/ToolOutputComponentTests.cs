using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for DiffViewerComponent, PhaseTransitionComponent, and ResearchDisplayComponent.
/// Covers AC: tool call output expansion, diff viewer, phase transitions, research display.
/// </summary>
public class ToolOutputComponentTests
{
    // ==================== DiffViewerComponent ====================

    private readonly DiffViewerComponent _diff = new();

    [Fact]
    public void DiffViewer_Name_IsCorrect()
    {
        Assert.Equal("DiffViewer", _diff.Name);
    }

    [Fact]
    public void DiffViewer_ShowsFilePathAndStats()
    {
        var data = CreateDiffData();
        var lines = _diff.Render(data, new ScreenRect(0, 0, 80, 15));

        Assert.Contains(lines, l => l.Contains("src/auth.ts") && l.Contains("+45") && l.Contains("-12"));
    }

    [Fact]
    public void DiffViewer_ShowsLineNumbers()
    {
        var data = CreateDiffData();
        var lines = _diff.Render(data, new ScreenRect(0, 0, 80, 15));

        // Line numbers visible in non-addition lines
        Assert.Contains(lines, l => l.Contains("│") && l.Contains("10"));
    }

    [Fact]
    public void DiffViewer_ShowsAddRemoveMarkers()
    {
        var data = CreateDiffData();
        var lines = _diff.Render(data, new ScreenRect(0, 0, 80, 15));

        Assert.Contains(lines, l => l.Contains("+ Added validateToken"));
        Assert.Contains(lines, l => l.Contains("- Removed oldCode"));
    }

    [Fact]
    public void DiffViewer_ZeroRegion_ReturnsEmpty()
    {
        Assert.Empty(_diff.Render(CreateDiffData(), new ScreenRect(0, 0, 0, 10)));
        Assert.Empty(_diff.Render(CreateDiffData(), new ScreenRect(0, 0, 80, 0)));
    }

    [Fact]
    public void DiffViewer_AllLinesSameWidth()
    {
        var lines = _diff.Render(CreateDiffData(), new ScreenRect(0, 0, 80, 15));

        foreach (var line in lines)
            Assert.Equal(80, line.Length);
    }

    // ==================== PhaseTransitionComponent ====================

    private readonly PhaseTransitionComponent _transition = new();

    [Fact]
    public void PhaseTransition_Name_IsCorrect()
    {
        Assert.Equal("PhaseTransition", _transition.Name);
    }

    [Fact]
    public void PhaseTransition_ShowsTransitionHeader()
    {
        var data = CreateTransitionData();
        var lines = _transition.Render(data, new ScreenRect(0, 0, 80, 15));

        Assert.Contains(lines, l => l.Contains("◆ Phase Transition: Planning → Building"));
    }

    [Fact]
    public void PhaseTransition_ShowsSections()
    {
        var data = CreateTransitionData();
        var lines = _transition.Render(data, new ScreenRect(0, 0, 80, 15));

        Assert.Contains(lines, l => l.Contains("▸ Research Findings"));
        Assert.Contains(lines, l => l.Contains("• JWT best practices identified"));
        Assert.Contains(lines, l => l.Contains("▸ Component Breakdown"));
        Assert.Contains(lines, l => l.Contains("• 3 components identified"));
    }

    [Fact]
    public void PhaseTransition_ZeroRegion_ReturnsEmpty()
    {
        Assert.Empty(_transition.Render(CreateTransitionData(), new ScreenRect(0, 0, 0, 10)));
    }

    // ==================== ResearchDisplayComponent ====================

    private readonly ResearchDisplayComponent _research = new();

    [Fact]
    public void ResearchDisplay_Name_IsCorrect()
    {
        Assert.Equal("ResearchDisplay", _research.Name);
    }

    [Fact]
    public void ResearchDisplay_ShowsTopic()
    {
        var data = CreateResearchData();
        var lines = _research.Render(data, new ScreenRect(0, 0, 80, 10));

        Assert.Contains(lines, l => l.Contains("📖 Research: JWT Authentication"));
    }

    [Fact]
    public void ResearchDisplay_ShowsFindings()
    {
        var data = CreateResearchData();
        var lines = _research.Render(data, new ScreenRect(0, 0, 80, 10));

        Assert.Contains(lines, l => l.Contains("Finding: Use RS256 for production"));
        Assert.Contains(lines, l => l.Contains("Finding: Token expiry should be 15 min"));
    }

    [Fact]
    public void ResearchDisplay_ShowsDocumentLink()
    {
        var data = CreateResearchData();
        var lines = _research.Render(data, new ScreenRect(0, 0, 80, 10));

        Assert.Contains(lines, l => l.Contains("[See full research document]"));
    }

    [Fact]
    public void ResearchDisplay_NoDocument_HidesLink()
    {
        var data = CreateResearchData() with { HasFullDocument = false };
        var lines = _research.Render(data, new ScreenRect(0, 0, 80, 10));

        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("[See full research document]"));
    }

    [Fact]
    public void ResearchDisplay_ZeroRegion_ReturnsEmpty()
    {
        Assert.Empty(_research.Render(CreateResearchData(), new ScreenRect(0, 0, 0, 10)));
    }

    // ==================== Helpers ====================

    private static DiffViewerData CreateDiffData() => new()
    {
        FilePath = "src/auth.ts",
        LinesAdded = 45,
        LinesRemoved = 12,
        Hunks =
        [
            new DiffHunk
            {
                StartLine = 10,
                Lines = [" import jwt from 'jsonwebtoken';", "+ Added validateToken", "- Removed oldCode", " export default;"],
            },
        ],
    };

    private static PhaseTransitionData CreateTransitionData() => new()
    {
        FromPhase = "Planning",
        ToPhase = "Building",
        Sections =
        [
            new TransitionSection("Research Findings", ["JWT best practices identified", "RS256 recommended"]),
            new TransitionSection("Component Breakdown", ["3 components identified", "auth-module, session-module, permission-module"]),
        ],
    };

    private static ResearchDisplayData CreateResearchData() => new()
    {
        Topic = "JWT Authentication",
        Findings = ["Use RS256 for production", "Token expiry should be 15 min"],
        HasFullDocument = true,
    };
}
