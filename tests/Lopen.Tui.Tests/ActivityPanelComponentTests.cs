using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for ActivityPanelComponent rendering.
/// Covers AC: main activity area with scrolling, progressive disclosure, expand/collapse.
/// </summary>
public class ActivityPanelComponentTests
{
    private readonly ActivityPanelComponent _component = new();

    // ==================== Component metadata ====================

    [Fact]
    public void Name_IsActivityPanel()
    {
        Assert.Equal("ActivityPanel", _component.Name);
    }

    // ==================== Progressive disclosure ====================

    [Fact]
    public void Render_CollapsedEntry_ShowsSummaryOnly()
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry
                {
                    Summary = "Edit src/auth.ts (+45 -12)",
                    Details = ["+ Added validateToken", "+ Imported JWT library"],
                    IsExpanded = false,
                },
            ],
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 5));

        Assert.Contains("Edit src/auth.ts", lines[0]);
        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("validateToken"));
    }

    [Fact]
    public void Render_ExpandedEntry_ShowsSummaryAndDetails()
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry
                {
                    Summary = "Edit src/auth.ts (+45 -12)",
                    Details = ["+ Added validateToken", "+ Imported JWT library"],
                    IsExpanded = true,
                },
            ],
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 5));

        Assert.Contains("Edit src/auth.ts", lines[0]);
        Assert.Contains(lines, l => l.Contains("validateToken"));
        Assert.Contains(lines, l => l.Contains("Imported JWT"));
    }

    [Fact]
    public void Render_MixedExpandCollapse_OnlyExpandedShowsDetails()
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry { Summary = "First action", Details = ["Detail A"], IsExpanded = false },
                new ActivityEntry { Summary = "Second action", Details = ["Detail B"], IsExpanded = true },
            ],
            ScrollOffset = 0,
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("Detail A"));
        Assert.Contains(lines, l => l.Contains("Detail B"));
    }

    // ==================== Scrolling ====================

    [Fact]
    public void Render_AutoScroll_ShowsLastEntries()
    {
        var entries = Enumerable.Range(1, 20)
            .Select(i => new ActivityEntry { Summary = $"Action {i}" })
            .ToList();

        var data = new ActivityPanelData { Entries = entries, ScrollOffset = -1 };
        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 5));

        // Should show last 5 entries
        Assert.Contains("Action 16", lines[0]);
        Assert.Contains("Action 20", lines[4]);
    }

    [Fact]
    public void Render_ScrollOffset0_ShowsFromTop()
    {
        var entries = Enumerable.Range(1, 20)
            .Select(i => new ActivityEntry { Summary = $"Action {i}" })
            .ToList();

        var data = new ActivityPanelData { Entries = entries, ScrollOffset = 0 };
        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 5));

        Assert.Contains("Action 1", lines[0]);
        Assert.Contains("Action 5", lines[4]);
    }

    [Fact]
    public void Render_ScrollOffsetMiddle_ShowsMiddleEntries()
    {
        var entries = Enumerable.Range(1, 20)
            .Select(i => new ActivityEntry { Summary = $"Action {i}" })
            .ToList();

        var data = new ActivityPanelData { Entries = entries, ScrollOffset = 5 };
        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 5));

        Assert.Contains("Action 6", lines[0]);
    }

    // ==================== Entry kind prefixes ====================

    [Theory]
    [InlineData(ActivityEntryKind.Action, "●")]
    [InlineData(ActivityEntryKind.FileEdit, "●")]
    [InlineData(ActivityEntryKind.Command, "$")]
    [InlineData(ActivityEntryKind.TestResult, "✓")]
    [InlineData(ActivityEntryKind.PhaseTransition, "◆")]
    [InlineData(ActivityEntryKind.Error, "⚠")]
    public void KindPrefix_ReturnsCorrectSymbol(ActivityEntryKind kind, string expected)
    {
        Assert.Equal(expected, ActivityPanelComponent.KindPrefix(kind));
    }

    [Fact]
    public void Render_CommandKind_UseDollarPrefix()
    {
        var data = new ActivityPanelData
        {
            Entries = [new ActivityEntry { Summary = "npm test", Kind = ActivityEntryKind.Command }],
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 3));

        Assert.Contains("$ npm test", lines[0]);
    }

    [Fact]
    public void Render_ErrorKind_UseWarningPrefix()
    {
        var data = new ActivityPanelData
        {
            Entries = [new ActivityEntry { Summary = "Build failed", Kind = ActivityEntryKind.Error }],
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 3));

        Assert.Contains("⚠ Build failed", lines[0]);
    }

    // ==================== CalculateTotalLines ====================

    [Fact]
    public void CalculateTotalLines_CollapsedOnly()
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry { Summary = "A", Details = ["d1", "d2"], IsExpanded = false },
                new ActivityEntry { Summary = "B", Details = ["d3"], IsExpanded = false },
            ],
        };

        Assert.Equal(2, ActivityPanelComponent.CalculateTotalLines(data));
    }

    [Fact]
    public void CalculateTotalLines_ExpandedIncludesDetails()
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry { Summary = "A", Details = ["d1", "d2"], IsExpanded = true },
                new ActivityEntry { Summary = "B", Details = ["d3"], IsExpanded = false },
            ],
        };

        Assert.Equal(4, ActivityPanelComponent.CalculateTotalLines(data)); // 1+2+1
    }

    // ==================== Edge cases ====================

    [Fact]
    public void Render_EmptyEntries_ReturnsPaddedLines()
    {
        var data = new ActivityPanelData();
        var lines = _component.Render(data, new ScreenRect(0, 0, 40, 3));

        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => Assert.Equal(40, l.Length));
    }

    [Fact]
    public void Render_ZeroWidth_ReturnsEmpty()
    {
        var data = new ActivityPanelData
        {
            Entries = [new ActivityEntry { Summary = "Test" }],
        };
        Assert.Empty(_component.Render(data, new ScreenRect(0, 0, 0, 5)));
    }

    [Fact]
    public void Render_ZeroHeight_ReturnsEmpty()
    {
        var data = new ActivityPanelData
        {
            Entries = [new ActivityEntry { Summary = "Test" }],
        };
        Assert.Empty(_component.Render(data, new ScreenRect(0, 0, 40, 0)));
    }

    [Fact]
    public void Render_AllLinesSameWidth()
    {
        var data = new ActivityPanelData
        {
            Entries =
            [
                new ActivityEntry { Summary = "Short", IsExpanded = true, Details = ["Detail line that is longer"] },
                new ActivityEntry { Summary = "Another" },
            ],
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 50, 10));

        foreach (var line in lines)
        {
            Assert.Equal(50, line.Length);
        }
    }
}
