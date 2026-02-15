using Lopen.Core.Workflow;

namespace Lopen.Core.Tests.Workflow;

public class WorkflowTriggerTests
{
    [Fact]
    public void WorkflowTrigger_HasExpectedTriggerCount()
    {
        var values = Enum.GetValues<WorkflowTrigger>();
        Assert.Equal(9, values.Length);
    }

    [Theory]
    [InlineData(WorkflowTrigger.Assess)]
    [InlineData(WorkflowTrigger.SpecApproved)]
    [InlineData(WorkflowTrigger.DependenciesDetermined)]
    [InlineData(WorkflowTrigger.ComponentsIdentified)]
    [InlineData(WorkflowTrigger.ComponentSelected)]
    [InlineData(WorkflowTrigger.TasksBrokenDown)]
    [InlineData(WorkflowTrigger.TaskIterationComplete)]
    [InlineData(WorkflowTrigger.ComponentComplete)]
    [InlineData(WorkflowTrigger.ModuleComplete)]
    public void WorkflowTrigger_IsDefined(WorkflowTrigger trigger)
    {
        Assert.True(Enum.IsDefined(trigger));
    }
}
