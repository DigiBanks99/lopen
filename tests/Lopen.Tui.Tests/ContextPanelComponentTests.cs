using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for ContextPanelComponent rendering.
/// Covers AC: context panel shows current task, task tree with completion states, active resources.
/// </summary>
public class ContextPanelComponentTests
{
    private static ContextPanelData CreateFullData() => new()
    {
        CurrentTask = new TaskSectionData
        {
            Name = "Implement JWT token validation",
            ProgressPercent = 60,
            CompletedSubtasks = 3,
            TotalSubtasks = 5,
            Subtasks =
            [
                new("Parse token from header", TaskState.Complete),
                new("Verify signature with secret", TaskState.Complete),
                new("Check expiration", TaskState.InProgress),
                new("Validate custom claims", TaskState.Pending),
                new("Handle edge cases & errors", TaskState.Pending),
            ],
        },
        Component = new ComponentSectionData
        {
            Name = "auth-module",
            CompletedTasks = 3,
            TotalTasks = 5,
            Tasks =
            [
                new("Setup JWT library", TaskState.Complete),
                new("Create token generator", TaskState.Complete),
                new("Token validation", TaskState.InProgress),
                new("Refresh token logic", TaskState.Pending),
                new("Integration tests", TaskState.Pending),
            ],
        },
        Module = new ModuleSectionData
        {
            Name = "authentication",
            InProgressComponents = 1,
            TotalComponents = 3,
            Components =
            [
                new("auth-module", TaskState.InProgress),
                new("session-module", TaskState.Pending),
                new("permission-module", TaskState.Pending),
            ],
        },
        Resources =
        [
            new("SPECIFICATION.md § Authentication"),
            new("research/jwt-best-practices.md"),
            new("plan.md § Security & Token Handling"),
        ],
    };

    private readonly ContextPanelComponent _component = new();

    // ==================== Component metadata ====================

    [Fact]
    public void Name_IsContextPanel()
    {
        Assert.Equal("ContextPanel", _component.Name);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_component.Description));
    }

    // ==================== Full render ====================

    [Fact]
    public void Render_FullData_ContainsCurrentTaskHeader()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        Assert.Contains(lines, l => l.Contains("▶ Current Task: Implement JWT"));
    }

    [Fact]
    public void Render_FullData_ContainsProgressLine()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        Assert.Contains(lines, l => l.Contains("60%") && l.Contains("3/5 subtasks"));
    }

    [Fact]
    public void Render_FullData_ContainsSubtaskTree()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        Assert.Contains(lines, l => l.Contains("├─✓ Parse token"));
        Assert.Contains(lines, l => l.Contains("├─▶ Check expiration"));
        Assert.Contains(lines, l => l.Contains("└─○ Handle edge cases"));
    }

    [Fact]
    public void Render_FullData_LastSubtaskUsesCorner()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        // Last subtask should use └─ connector
        Assert.Contains(lines, l => l.Contains("└─○ Handle edge cases"));
    }

    [Fact]
    public void Render_FullData_ContainsComponentSection()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        Assert.Contains(lines, l => l.Contains("📊 Component: auth-module"));
        Assert.Contains(lines, l => l.Contains("Tasks: 3/5 complete"));
    }

    [Fact]
    public void Render_FullData_ContainsModuleSection()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        Assert.Contains(lines, l => l.Contains("📦 Module: authentication"));
        Assert.Contains(lines, l => l.Contains("Components: 1/3 in progress"));
    }

    [Fact]
    public void Render_FullData_ContainsResourcesSection()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        Assert.Contains(lines, l => l.Contains("📚 Active Resources:"));
        Assert.Contains(lines, l => l.Contains("[1] SPECIFICATION.md § Authentication"));
        Assert.Contains(lines, l => l.Contains("[2] research/jwt-best-practices.md"));
        Assert.Contains(lines, l => l.Contains("[3] plan.md § Security & Token Handling"));
        Assert.Contains(lines, l => l.Contains("Press 1-9 to view"));
    }

    // ==================== Partial data ====================

    [Fact]
    public void Render_TaskOnly_OmitsOtherSections()
    {
        var data = new ContextPanelData
        {
            CurrentTask = new TaskSectionData
            {
                Name = "Test task",
                ProgressPercent = 50,
                CompletedSubtasks = 1,
                TotalSubtasks = 2,
                Subtasks = [new("Sub A", TaskState.Complete), new("Sub B", TaskState.Pending)],
            },
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("▶ Current Task: Test task"));
        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("📊"));
        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("📦"));
        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("📚"));
    }

    [Fact]
    public void Render_EmptyData_ReturnsBlankLines()
    {
        var data = new ContextPanelData();
        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 5));

        Assert.Equal(5, lines.Length);
        Assert.All(lines, l => Assert.Equal(60, l.Length));
        Assert.All(lines, l => Assert.True(string.IsNullOrWhiteSpace(l)));
    }

    // ==================== Edge cases ====================

    [Fact]
    public void Render_ZeroWidth_ReturnsEmpty()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 0, 10));
        Assert.Empty(lines);
    }

    [Fact]
    public void Render_ZeroHeight_ReturnsEmpty()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 0));
        Assert.Empty(lines);
    }

    [Fact]
    public void Render_AllLinesSameWidth()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 30));

        foreach (var line in lines)
        {
            Assert.Equal(80, line.Length);
        }
    }

    [Fact]
    public void Render_TruncatedHeight_OnlyShowsTopLines()
    {
        var lines = _component.Render(CreateFullData(), new ScreenRect(0, 0, 80, 3));

        Assert.Equal(3, lines.Length);
        Assert.Contains("▶ Current Task", lines[0]);
    }

    // ==================== StateIcon ====================

    [Theory]
    [InlineData(TaskState.Pending, "○")]
    [InlineData(TaskState.InProgress, "▶")]
    [InlineData(TaskState.Complete, "✓")]
    [InlineData(TaskState.Failed, "✗")]
    public void StateIcon_ReturnsCorrectSymbol(TaskState state, string expected)
    {
        Assert.Equal(expected, ContextPanelComponent.StateIcon(state));
    }

    // ==================== Failed state ====================

    [Fact]
    public void Render_FailedSubtask_ShowsFailedIcon()
    {
        var data = new ContextPanelData
        {
            CurrentTask = new TaskSectionData
            {
                Name = "Failing task",
                ProgressPercent = 25,
                CompletedSubtasks = 1,
                TotalSubtasks = 4,
                Subtasks =
                [
                    new("Done step", TaskState.Complete),
                    new("Failed step", TaskState.Failed),
                    new("Blocked step", TaskState.Pending),
                    new("Later step", TaskState.Pending),
                ],
            },
        };

        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("├─✗ Failed step"));
    }

    // ==================== Resources limit ====================

    [Fact]
    public void Render_MoreThan9Resources_OnlyShows9()
    {
        var resources = Enumerable.Range(1, 12)
            .Select(i => new ResourceItem($"doc{i}.md"))
            .ToList();

        var data = new ContextPanelData { Resources = resources };
        var lines = _component.Render(data, new ScreenRect(0, 0, 60, 20));

        // Should show [1] through [9] but not [10]
        Assert.Contains(lines, l => l.Contains("[9] doc9.md"));
        Assert.DoesNotContain(lines, l => l.TrimEnd().Contains("[10]"));
    }
}
