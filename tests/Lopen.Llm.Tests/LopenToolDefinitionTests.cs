namespace Lopen.Llm.Tests;

public class LopenToolDefinitionTests
{
    [Fact]
    public void LopenToolDefinition_MinimalCreation()
    {
        var tool = new LopenToolDefinition("read_spec", "Read a specification section");

        Assert.Equal("read_spec", tool.Name);
        Assert.Equal("Read a specification section", tool.Description);
        Assert.Null(tool.ParameterSchema);
        Assert.Null(tool.AvailableInPhases);
    }

    [Fact]
    public void LopenToolDefinition_WithAllProperties()
    {
        var phases = new List<WorkflowPhase> { WorkflowPhase.Building, WorkflowPhase.Research };
        var tool = new LopenToolDefinition(
            Name: "verify_task_completion",
            Description: "Verify a task is complete",
            ParameterSchema: "{\"type\":\"object\"}",
            AvailableInPhases: phases);

        Assert.Equal("verify_task_completion", tool.Name);
        Assert.Equal("{\"type\":\"object\"}", tool.ParameterSchema);
        Assert.Equal(2, tool.AvailableInPhases!.Count);
        Assert.Contains(WorkflowPhase.Building, tool.AvailableInPhases);
    }

    [Fact]
    public void LopenToolDefinition_EqualityByValue()
    {
        var a = new LopenToolDefinition("read_spec", "Read a spec");
        var b = new LopenToolDefinition("read_spec", "Read a spec");

        Assert.Equal(a, b);
    }

    [Fact]
    public void LopenToolDefinition_InequalityWhenDifferent()
    {
        var a = new LopenToolDefinition("read_spec", "Read a spec");
        var b = new LopenToolDefinition("read_plan", "Read a plan");

        Assert.NotEqual(a, b);
    }
}
