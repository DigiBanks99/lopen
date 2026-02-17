using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public sealed class DefaultToolRegistryTests
{
    private readonly DefaultToolRegistry _registry = new(NullLogger<DefaultToolRegistry>.Instance);

    [Fact]
    public void GetAllTools_Returns10BuiltInTools()
    {
        var tools = _registry.GetAllTools();

        Assert.Equal(10, tools.Count);
    }

    [Theory]
    [InlineData("read_spec")]
    [InlineData("read_research")]
    [InlineData("read_plan")]
    [InlineData("update_task_status")]
    [InlineData("get_current_context")]
    [InlineData("log_research")]
    [InlineData("report_progress")]
    [InlineData("verify_task_completion")]
    [InlineData("verify_component_completion")]
    [InlineData("verify_module_completion")]
    public void GetAllTools_ContainsTool(string toolName)
    {
        var tools = _registry.GetAllTools();

        Assert.Contains(tools, t => t.Name == toolName);
    }

    [Fact]
    public void GetToolsForPhase_Building_IncludesVerificationTools()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Building);

        Assert.Contains(tools, t => t.Name == "verify_task_completion");
        Assert.Contains(tools, t => t.Name == "verify_component_completion");
        Assert.Contains(tools, t => t.Name == "verify_module_completion");
        Assert.Contains(tools, t => t.Name == "update_task_status");
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering)]
    [InlineData(WorkflowPhase.Planning)]
    [InlineData(WorkflowPhase.Research)]
    public void GetToolsForPhase_NonBuilding_ExcludesVerificationTools(WorkflowPhase phase)
    {
        var tools = _registry.GetToolsForPhase(phase);

        Assert.DoesNotContain(tools, t => t.Name == "verify_task_completion");
        Assert.DoesNotContain(tools, t => t.Name == "verify_component_completion");
        Assert.DoesNotContain(tools, t => t.Name == "verify_module_completion");
    }

    [Fact]
    public void GetToolsForPhase_Research_IncludesLogResearch()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Research);

        Assert.Contains(tools, t => t.Name == "log_research");
    }

    [Fact]
    public void GetToolsForPhase_Building_ExcludesLogResearch()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Building);

        Assert.DoesNotContain(tools, t => t.Name == "log_research");
    }

    [Fact]
    public void GetToolsForPhase_RequirementGathering_IncludesLogResearch()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.RequirementGathering);

        Assert.Contains(tools, t => t.Name == "log_research");
    }

    [Fact]
    public void GetToolsForPhase_Planning_ExcludesUpdateTaskStatus()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Planning);

        Assert.DoesNotContain(tools, t => t.Name == "update_task_status");
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering)]
    [InlineData(WorkflowPhase.Planning)]
    [InlineData(WorkflowPhase.Building)]
    [InlineData(WorkflowPhase.Research)]
    public void GetToolsForPhase_AllPhases_IncludeReadSpec(WorkflowPhase phase)
    {
        var tools = _registry.GetToolsForPhase(phase);

        Assert.Contains(tools, t => t.Name == "read_spec");
    }

    [Fact]
    public void RegisterTool_AddsCustomTool()
    {
        var custom = new LopenToolDefinition("custom_tool", "A custom tool");

        _registry.RegisterTool(custom);

        Assert.Equal(11, _registry.GetAllTools().Count);
        Assert.Contains(_registry.GetAllTools(), t => t.Name == "custom_tool");
    }

    [Fact]
    public void RegisterTool_WithPhaseFilter_RespectedByGetToolsForPhase()
    {
        var custom = new LopenToolDefinition(
            "build_only_tool", "Build only",
            AvailableInPhases: [WorkflowPhase.Building]);

        _registry.RegisterTool(custom);

        Assert.Contains(_registry.GetToolsForPhase(WorkflowPhase.Building), t => t.Name == "build_only_tool");
        Assert.DoesNotContain(_registry.GetToolsForPhase(WorkflowPhase.Research), t => t.Name == "build_only_tool");
    }

    [Fact]
    public void RegisterTool_Duplicate_SkipsSecondRegistration()
    {
        var duplicate = new LopenToolDefinition("read_spec", "duplicate");

        _registry.RegisterTool(duplicate);

        Assert.Equal(10, _registry.GetAllTools().Count);
    }

    [Fact]
    public void RegisterTool_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.RegisterTool(null!));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultToolRegistry(null!));
    }

    [Fact]
    public void GetToolsForPhase_Planning_IncludesReadPlan()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Planning);

        Assert.Contains(tools, t => t.Name == "read_plan");
    }

    [Fact]
    public void GetToolsForPhase_Research_ExcludesReadPlan()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Research);

        Assert.DoesNotContain(tools, t => t.Name == "read_plan");
    }

    [Fact]
    public void GetToolsForPhase_Building_IncludesReadPlan()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Building);

        Assert.Contains(tools, t => t.Name == "read_plan");
    }

    [Fact]
    public void GetToolsForPhase_RequirementGathering_ExcludesReadPlan()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.RequirementGathering);

        Assert.DoesNotContain(tools, t => t.Name == "read_plan");
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering)]
    [InlineData(WorkflowPhase.Planning)]
    [InlineData(WorkflowPhase.Building)]
    [InlineData(WorkflowPhase.Research)]
    public void GetToolsForPhase_AllPhases_IncludeReportProgress(WorkflowPhase phase)
    {
        var tools = _registry.GetToolsForPhase(phase);

        Assert.Contains(tools, t => t.Name == "report_progress");
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering)]
    [InlineData(WorkflowPhase.Planning)]
    [InlineData(WorkflowPhase.Building)]
    [InlineData(WorkflowPhase.Research)]
    public void GetToolsForPhase_AllPhases_IncludeGetCurrentContext(WorkflowPhase phase)
    {
        var tools = _registry.GetToolsForPhase(phase);

        Assert.Contains(tools, t => t.Name == "get_current_context");
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering)]
    [InlineData(WorkflowPhase.Planning)]
    [InlineData(WorkflowPhase.Building)]
    [InlineData(WorkflowPhase.Research)]
    public void GetToolsForPhase_AllPhases_IncludeReadResearch(WorkflowPhase phase)
    {
        var tools = _registry.GetToolsForPhase(phase);

        Assert.Contains(tools, t => t.Name == "read_research");
    }

    [Fact]
    public void GetToolsForPhase_RequirementGathering_ExcludesUpdateTaskStatus()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.RequirementGathering);

        Assert.DoesNotContain(tools, t => t.Name == "update_task_status");
    }

    [Fact]
    public void GetToolsForPhase_Research_ExcludesUpdateTaskStatus()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Research);

        Assert.DoesNotContain(tools, t => t.Name == "update_task_status");
    }

    [Fact]
    public void GetToolsForPhase_Planning_ExcludesLogResearch()
    {
        var tools = _registry.GetToolsForPhase(WorkflowPhase.Planning);

        Assert.DoesNotContain(tools, t => t.Name == "log_research");
    }
}
