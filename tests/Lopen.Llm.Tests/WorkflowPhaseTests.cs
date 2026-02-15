namespace Lopen.Llm.Tests;

public class WorkflowPhaseTests
{
    [Fact]
    public void WorkflowPhase_HasFourValues()
    {
        var values = Enum.GetValues<WorkflowPhase>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering, 0)]
    [InlineData(WorkflowPhase.Planning, 1)]
    [InlineData(WorkflowPhase.Building, 2)]
    [InlineData(WorkflowPhase.Research, 3)]
    public void WorkflowPhase_HasExpectedOrdinal(WorkflowPhase phase, int expected)
    {
        Assert.Equal(expected, (int)phase);
    }

    [Theory]
    [InlineData("RequirementGathering", WorkflowPhase.RequirementGathering)]
    [InlineData("Planning", WorkflowPhase.Planning)]
    [InlineData("Building", WorkflowPhase.Building)]
    [InlineData("Research", WorkflowPhase.Research)]
    public void WorkflowPhase_ParsesFromString(string name, WorkflowPhase expected)
    {
        Assert.Equal(expected, Enum.Parse<WorkflowPhase>(name));
    }

    [Fact]
    public void WorkflowPhase_InvalidValue_ThrowsOnParse()
    {
        Assert.Throws<ArgumentException>(() => Enum.Parse<WorkflowPhase>("Invalid"));
    }
}
