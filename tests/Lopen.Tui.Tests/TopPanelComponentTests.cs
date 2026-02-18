using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for TopPanelComponent rendering (TUI-02, TUI-17, TUI-29, TUI-30).
/// Covers AC: top panel displays logo, version, model, context usage,
/// premium requests, git branch, auth status, phase, and step.
/// </summary>
public class TopPanelComponentTests
{
    private static TopPanelData CreateDefaultData() => new()
    {
        Version = "v1.0.0",
        ModelName = "claude-opus-4.6",
        ContextUsedTokens = 2400,
        ContextMaxTokens = 128_000,
        PremiumRequestCount = 23,
        GitBranch = "main",
        IsAuthenticated = true,
        PhaseName = "Building",
        CurrentStep = 6,
        TotalSteps = 7,
        StepLabel = "Iterate Tasks",
        ShowLogo = true,
    };

    private readonly TopPanelComponent _component = new();

    // ==================== Component metadata ====================

    [Fact]
    public void Name_IsTopPanel()
    {
        Assert.Equal("TopPanel", _component.Name);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_component.Description));
    }

    // ==================== TUI-02: Top Panel Display ====================

    // ==================== Render with logo ====================

    [Fact]
    public void Render_WithLogo_Returns3ContentLines()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Equal(4, lines.Length);
        // Row 1: logo line 1 + version + status
        Assert.Contains("╻  ┏━┓┏━┓┏━╸┏┓╻", lines[0]);
        Assert.Contains("v1.0.0", lines[0]);
    }

    [Fact]
    public void Render_WithLogo_ShowsModelName()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("claude-opus-4.6", lines[0]);
    }

    [Fact]
    public void Render_WithLogo_ShowsContextUsage()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("Context: 2.4K/128K", lines[0]);
    }

    [Fact]
    public void Render_WithLogo_ShowsPremiumRequests()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("23 premium", lines[0]);
    }

    [Fact]
    public void Render_WithLogo_ShowsGitBranch()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("main", lines[0]);
    }

    [Fact]
    public void Render_WithLogo_ShowsAuthenticatedStatus()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("🟢", lines[0]);
    }

    [Fact]
    public void Render_WithLogo_ShowsPhaseAndStep()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("Phase: Building", lines[2]);
        Assert.Contains("●●●●●●○", lines[2]);
        Assert.Contains("Step 6/7", lines[2]);
        Assert.Contains("Iterate Tasks", lines[2]);
    }

    // ==================== Render without logo ====================

    [Fact]
    public void Render_WithoutLogo_OmitsLogoLines()
    {
        var data = CreateDefaultData() with { ShowLogo = false };
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.DoesNotContain("┏━┓", lines[0]);
        Assert.Contains("v1.0.0", lines[0]);
    }

    [Fact]
    public void Render_WithoutLogo_ShowsPhaseLine()
    {
        var data = CreateDefaultData() with { ShowLogo = false };
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("Phase: Building", lines[1]);
    }

    // ==================== Auth status ====================

    [Fact]
    public void Render_Unauthenticated_ShowsRedCircle()
    {
        var data = CreateDefaultData() with { IsAuthenticated = false };
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.Contains("🔴", lines[0]);
        Assert.DoesNotContain("🟢", lines[0]);
    }

    // ==================== TUI-30: Premium Request Counter (🔥 Indicator) ====================

    // ==================== No premium requests ====================

    [Fact]
    public void Render_ZeroPremium_OmitsPremiumSection()
    {
        var data = CreateDefaultData() with { PremiumRequestCount = 0 };
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        Assert.DoesNotContain("premium", lines[0]);
    }

    // ==================== No git branch ====================

    [Fact]
    public void Render_NoBranch_OmitsBranch()
    {
        var data = CreateDefaultData() with { GitBranch = null };
        var region = new ScreenRect(0, 0, 120, 4);

        var lines = _component.Render(data, region);

        // Status line should not have "main"
        var statusLine = TopPanelComponent.BuildStatusLine(data with { GitBranch = null });
        Assert.DoesNotContain("main", statusLine);
    }

    // ==================== Empty region ====================

    [Fact]
    public void Render_ZeroWidth_ReturnsEmpty()
    {
        var data = CreateDefaultData();
        var lines = _component.Render(data, new ScreenRect(0, 0, 0, 4));
        Assert.Empty(lines);
    }

    [Fact]
    public void Render_ZeroHeight_ReturnsEmpty()
    {
        var data = CreateDefaultData();
        var lines = _component.Render(data, new ScreenRect(0, 0, 120, 0));
        Assert.Empty(lines);
    }

    // ==================== TUI-29: Context Window Usage ====================

    // ==================== FormatTokens ====================

    [Theory]
    [InlineData(0, "0")]
    [InlineData(500, "500")]
    [InlineData(999, "999")]
    [InlineData(1000, "1K")]
    [InlineData(2400, "2.4K")]
    [InlineData(128_000, "128K")]
    [InlineData(1_000_000, "1M")]
    [InlineData(1_500_000, "1.5M")]
    public void FormatTokens_VariousValues(long tokens, string expected)
    {
        Assert.Equal(expected, TopPanelComponent.FormatTokens(tokens));
    }

    // ==================== TUI-17: Phase/Step Visualization (●/○ Progress Indicator) ====================

    // ==================== BuildStepIndicator ====================

    [Theory]
    [InlineData(0, 7, "○○○○○○○")]
    [InlineData(3, 7, "●●●○○○○")]
    [InlineData(7, 7, "●●●●●●●")]
    [InlineData(1, 1, "●")]
    public void BuildStepIndicator_VariousSteps(int current, int total, string expected)
    {
        Assert.Equal(expected, TopPanelComponent.BuildStepIndicator(current, total));
    }

    [Fact]
    public void BuildStepIndicator_ZeroTotal_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TopPanelComponent.BuildStepIndicator(0, 0));
    }

    // ==================== BuildPhaseLine ====================

    [Fact]
    public void BuildPhaseLine_NullPhase_ReturnsEmpty()
    {
        var data = CreateDefaultData() with { PhaseName = null };
        Assert.Equal(string.Empty, TopPanelComponent.BuildPhaseLine(data));
    }

    [Fact]
    public void BuildPhaseLine_WithPhaseAndSteps_ContainsAll()
    {
        var data = CreateDefaultData();
        var line = TopPanelComponent.BuildPhaseLine(data);

        Assert.Contains("Phase: Building", line);
        Assert.Contains("●●●●●●○", line);
        Assert.Contains("Step 6/7", line);
        Assert.Contains("Iterate Tasks", line);
    }

    // ==================== BuildStatusLine ====================

    [Fact]
    public void BuildStatusLine_ContainsAllSections()
    {
        var data = CreateDefaultData();
        var line = TopPanelComponent.BuildStatusLine(data);

        Assert.Contains("claude-opus-4.6", line);
        Assert.Contains("Context: 2.4K/128K", line);
        Assert.Contains("23 premium", line);
        Assert.Contains("main", line);
        Assert.Contains("🟢", line);
    }

    [Fact]
    public void BuildStatusLine_NoModel_OmitsModel()
    {
        var data = CreateDefaultData() with { ModelName = null };
        var line = TopPanelComponent.BuildStatusLine(data);

        Assert.DoesNotContain("claude", line);
        Assert.Contains("Context:", line);
    }

    // ==================== Lines padded to width ====================

    [Fact]
    public void Render_AllLinesSameWidth()
    {
        var data = CreateDefaultData();
        var region = new ScreenRect(0, 0, 100, 4);

        var lines = _component.Render(data, region);

        foreach (var line in lines)
        {
            Assert.Equal(100, line.Length);
        }
    }
}
