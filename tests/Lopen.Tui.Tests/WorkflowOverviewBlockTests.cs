using Lopen.Configuration;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Lopen.Tui.Tests;

public class WorkflowOverviewBlockTests
{
    private static IAnsiConsole CreateTestConsole() => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(TextWriter.Null),
    });

    // ── Phase Mapping Tests ──────────────────────────────────────────

    [Fact]
    public void MapPhases_RequirementGathering_AssessIsActive()
    {
        var phases = WorkflowOverviewBlock.MapPhases(WorkflowPhase.RequirementGathering, false);

        Assert.Equal(4, phases.Count);
        Assert.Equal(DisplayPhaseState.Active, phases[0].State);  // Assess
        Assert.Equal(DisplayPhaseState.Pending, phases[1].State); // Plan
        Assert.Equal(DisplayPhaseState.Pending, phases[2].State); // Build
        Assert.Equal(DisplayPhaseState.Pending, phases[3].State); // Verify
    }

    [Fact]
    public void MapPhases_Planning_PlanIsActive()
    {
        var phases = WorkflowOverviewBlock.MapPhases(WorkflowPhase.Planning, false);

        Assert.Equal(DisplayPhaseState.Complete, phases[0].State); // Assess
        Assert.Equal(DisplayPhaseState.Active, phases[1].State);   // Plan
        Assert.Equal(DisplayPhaseState.Pending, phases[2].State);  // Build
        Assert.Equal(DisplayPhaseState.Pending, phases[3].State);  // Verify
    }

    [Fact]
    public void MapPhases_Building_BuildIsActive()
    {
        var phases = WorkflowOverviewBlock.MapPhases(WorkflowPhase.Building, false);

        Assert.Equal(DisplayPhaseState.Complete, phases[0].State);
        Assert.Equal(DisplayPhaseState.Complete, phases[1].State);
        Assert.Equal(DisplayPhaseState.Active, phases[2].State);
        Assert.Equal(DisplayPhaseState.Pending, phases[3].State);
    }

    [Fact]
    public void MapPhases_IsComplete_AllComplete()
    {
        var phases = WorkflowOverviewBlock.MapPhases(WorkflowPhase.Building, true);

        Assert.All(phases, p => Assert.Equal(DisplayPhaseState.Complete, p.State));
    }

    [Theory]
    [InlineData("Assess")]
    [InlineData("Plan")]
    [InlineData("Build")]
    [InlineData("Verify")]
    public void MapPhases_AllFourPhasesNamed(string expectedName)
    {
        var phases = WorkflowOverviewBlock.MapPhases(WorkflowPhase.RequirementGathering, false);
        Assert.Contains(phases, p => p.Name == expectedName);
    }

    // ── Phase Display Tests ──────────────────────────────────────────

    [Fact]
    public void BuildPhaseDisplay_ContainsCheckForComplete()
    {
        var display = WorkflowOverviewBlock.BuildPhaseDisplay(WorkflowPhase.Building, false);
        Assert.Contains(LopenTheme.PhaseComplete, display);  // Assess and Plan should be ✓
        Assert.Contains(LopenTheme.PhaseActive, display);    // Build should be ●
        Assert.Contains(LopenTheme.PhasePending, display);   // Verify should be ○
    }

    // ── Token Formatting Tests ───────────────────────────────────────

    [Theory]
    [InlineData(0, "0")]
    [InlineData(500, "500")]
    [InlineData(1000, "1k")]
    [InlineData(1200, "1.2k")]
    [InlineData(50000, "50k")]
    [InlineData(1000000, "1M")]
    [InlineData(1500000, "1.5M")]
    public void FormatTokenCount_FormatsCorrectly(int count, string expected)
    {
        Assert.Equal(expected, WorkflowOverviewBlock.FormatTokenCount(count));
    }

    // ── Task Counting Tests ──────────────────────────────────────────

    [Fact]
    public void CountTasks_EmptyList_ReturnsZero()
    {
        var (completed, total, active) = WorkflowOverviewBlock.CountTasks([]);
        Assert.Equal(0, completed);
        Assert.Equal(0, total);
        Assert.Null(active);
    }

    [Fact]
    public void CountTasks_MixedStates_CountsCorrectly()
    {
        var tasks = new List<TaskHierarchyNode>
        {
            new() { Id = "1", Name = "task-one", State = "Complete", NodeType = "task" },
            new() { Id = "2", Name = "task-two", State = "InProgress", NodeType = "task" },
            new() { Id = "3", Name = "task-three", State = "Pending", NodeType = "task" },
        };

        var (completed, total, active) = WorkflowOverviewBlock.CountTasks(tasks);
        Assert.Equal(1, completed);
        Assert.Equal(3, total);
        Assert.Equal("task-two", active);
    }

    [Fact]
    public void CountTasks_NestedChildren_CountsRecursively()
    {
        var tasks = new List<TaskHierarchyNode>
        {
            new()
            {
                Id = "comp1", Name = "component", State = "InProgress", NodeType = "component",
                Children =
                [
                    new() { Id = "t1", Name = "sub-task-1", State = "Complete", NodeType = "task" },
                    new() { Id = "t2", Name = "sub-task-2", State = "InProgress", NodeType = "task" },
                ]
            },
        };

        var (completed, total, active) = WorkflowOverviewBlock.CountTasks(tasks);
        Assert.Equal(1, completed);
        Assert.Equal(2, total);
        Assert.Equal("sub-task-2", active);
    }

    // ── Render Tests ─────────────────────────────────────────────────

    [Fact]
    public void Render_NoWorkflow_DoesNotThrow()
    {
        var block = CreateBlock();
        block.Render(); // Should not throw
    }

    [Fact]
    public void Render_WithSessionState_DoesNotThrow()
    {
        var block = CreateBlock();
        var state = new SessionState
        {
            SessionId = "test-session",
            Phase = "Building",
            Step = "IterateThroughTasks",
            Module = "auth-module",
            Component = "login",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TaskHierarchy =
            [
                new() { Id = "t1", Name = "add-jwt", State = "Complete", NodeType = "task" },
                new() { Id = "t2", Name = "add-refresh", State = "InProgress", NodeType = "task" },
                new() { Id = "t3", Name = "add-logout", State = "Pending", NodeType = "task" },
            ],
        };

        block.Render(state); // Should not throw
    }

    private static WorkflowOverviewBlock CreateBlock()
    {
        var console = CreateTestConsole();
        var options = Options.Create(new LopenOptions());
        return new WorkflowOverviewBlock(console, options);
    }
}
