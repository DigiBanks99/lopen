using Lopen.Core.Workflow;

namespace Lopen.Core.Tests.Workflow;

public class WorkflowStepTests
{
    [Fact]
    public void WorkflowStep_HasSevenSteps()
    {
        var values = Enum.GetValues<WorkflowStep>();
        Assert.Equal(7, values.Length);
    }

    [Theory]
    [InlineData(WorkflowStep.DraftSpecification, 0)]
    [InlineData(WorkflowStep.DetermineDependencies, 1)]
    [InlineData(WorkflowStep.IdentifyComponents, 2)]
    [InlineData(WorkflowStep.SelectNextComponent, 3)]
    [InlineData(WorkflowStep.BreakIntoTasks, 4)]
    [InlineData(WorkflowStep.IterateThroughTasks, 5)]
    [InlineData(WorkflowStep.Repeat, 6)]
    public void WorkflowStep_HasExpectedValues(WorkflowStep step, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)step);
    }

    [Theory]
    [InlineData(WorkflowStep.DraftSpecification)]
    [InlineData(WorkflowStep.DetermineDependencies)]
    [InlineData(WorkflowStep.IdentifyComponents)]
    [InlineData(WorkflowStep.SelectNextComponent)]
    [InlineData(WorkflowStep.BreakIntoTasks)]
    [InlineData(WorkflowStep.IterateThroughTasks)]
    [InlineData(WorkflowStep.Repeat)]
    public void WorkflowStep_IsDefined(WorkflowStep step)
    {
        Assert.True(Enum.IsDefined(step));
    }
}
