using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for LayoutCalculator split-screen layout (TUI-01).
/// Covers AC: split-screen layout with activity (left) and context (right) panes.
/// </summary>
public class LayoutCalculatorTests
{
    // ==================== TUI-01: Split-Screen Layout ====================

    [Fact]
    public void Calculate_Default_ReturnsCorrectRegions()
    {
        var regions = LayoutCalculator.Calculate(120, 40);

        // Header: full width, 4 rows
        Assert.Equal(new ScreenRect(0, 0, 120, 4), regions.Header);

        // Body height: 40 - 4 - 3 = 33
        // Activity: 60% of 120 = 72
        Assert.Equal(new ScreenRect(0, 4, 72, 33), regions.Activity);

        // Context: 120 - 72 = 48
        Assert.Equal(new ScreenRect(72, 4, 48, 33), regions.Context);

        // Prompt: full width, bottom 3 rows
        Assert.Equal(new ScreenRect(0, 37, 120, 3), regions.Prompt);
    }

    [Fact]
    public void Calculate_50Percent_SplitsEvenly()
    {
        var regions = LayoutCalculator.Calculate(100, 30, splitPercent: 50);

        Assert.Equal(50, regions.Activity.Width);
        Assert.Equal(50, regions.Context.Width);
    }

    [Fact]
    public void Calculate_80Percent_MaximizesActivity()
    {
        var regions = LayoutCalculator.Calculate(100, 30, splitPercent: 80);

        Assert.Equal(80, regions.Activity.Width);
        Assert.Equal(20, regions.Context.Width);
    }

    [Fact]
    public void Calculate_BelowMin_ClampsTo50()
    {
        var regions = LayoutCalculator.Calculate(100, 30, splitPercent: 30);

        Assert.Equal(50, regions.Activity.Width);
        Assert.Equal(50, regions.Context.Width);
    }

    [Fact]
    public void Calculate_AboveMax_ClampsTo80()
    {
        var regions = LayoutCalculator.Calculate(100, 30, splitPercent: 95);

        Assert.Equal(80, regions.Activity.Width);
        Assert.Equal(20, regions.Context.Width);
    }

    [Fact]
    public void Calculate_ActivityAndContextWidths_SumToScreenWidth()
    {
        for (int pct = 0; pct <= 100; pct += 5)
        {
            var regions = LayoutCalculator.Calculate(120, 40, splitPercent: pct);

            Assert.Equal(120, regions.Activity.Width + regions.Context.Width);
        }
    }

    [Fact]
    public void Calculate_HeaderAndPromptHeights_CustomValues()
    {
        var regions = LayoutCalculator.Calculate(80, 24, headerHeight: 2, promptHeight: 1);

        Assert.Equal(2, regions.Header.Height);
        Assert.Equal(1, regions.Prompt.Height);
        Assert.Equal(21, regions.Activity.Height); // 24 - 2 - 1
        Assert.Equal(21, regions.Context.Height);
    }

    [Fact]
    public void Calculate_SmallScreen_BodyNeverNegative()
    {
        var regions = LayoutCalculator.Calculate(40, 5);

        // 5 - 4 (header) - 3 (prompt) = -2, clamped to 0
        Assert.True(regions.Activity.Height >= 0);
        Assert.True(regions.Context.Height >= 0);
    }

    [Fact]
    public void Calculate_Activity_StartsAtHeaderBottom()
    {
        var regions = LayoutCalculator.Calculate(100, 30, headerHeight: 6);

        Assert.Equal(6, regions.Activity.Y);
        Assert.Equal(6, regions.Context.Y);
    }

    [Fact]
    public void Calculate_Prompt_StartsAfterBody()
    {
        var regions = LayoutCalculator.Calculate(100, 30);

        Assert.Equal(regions.Activity.Y + regions.Activity.Height, regions.Prompt.Y);
    }

    [Fact]
    public void Calculate_ContextStartsAfterActivity()
    {
        var regions = LayoutCalculator.Calculate(100, 30, splitPercent: 60);

        Assert.Equal(regions.Activity.X + regions.Activity.Width, regions.Context.X);
    }

    [Fact]
    public void Calculate_AllRegions_HaveNonNegativeDimensions()
    {
        var regions = LayoutCalculator.Calculate(10, 3, splitPercent: 60);

        Assert.True(regions.Header.Width >= 0);
        Assert.True(regions.Header.Height >= 0);
        Assert.True(regions.Activity.Width >= 0);
        Assert.True(regions.Activity.Height >= 0);
        Assert.True(regions.Context.Width >= 0);
        Assert.True(regions.Context.Height >= 0);
        Assert.True(regions.Prompt.Width >= 0);
        Assert.True(regions.Prompt.Height >= 0);
    }
}
