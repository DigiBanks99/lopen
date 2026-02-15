using Lopen.Core.Tasks;

namespace Lopen.Core.Tests.Tasks;

public class WorkNodeExtensionsTests
{
    [Fact]
    public void Descendants_ReturnsAllDescendants()
    {
        var module = new ModuleNode("mod1", "Auth");
        var comp1 = new ComponentNode("c1", "JWT");
        var comp2 = new ComponentNode("c2", "Session");
        var task1 = new TaskNode("t1", "Validate");
        var subtask1 = new SubtaskNode("s1", "Parse");

        module.AddChild(comp1);
        module.AddChild(comp2);
        comp1.AddChild(task1);
        task1.AddChild(subtask1);

        var descendants = module.Descendants().ToList();

        Assert.Equal(4, descendants.Count);
        Assert.Contains(comp1, descendants);
        Assert.Contains(comp2, descendants);
        Assert.Contains(task1, descendants);
        Assert.Contains(subtask1, descendants);
    }

    [Fact]
    public void Descendants_EmptyForLeaf()
    {
        var subtask = new SubtaskNode("s1", "Parse");
        Assert.Empty(subtask.Descendants());
    }

    [Fact]
    public void Leaves_ReturnsOnlyLeafNodes()
    {
        var module = new ModuleNode("mod1", "Auth");
        var comp = new ComponentNode("c1", "JWT");
        var task = new TaskNode("t1", "Validate");
        var sub1 = new SubtaskNode("s1", "Parse");
        var sub2 = new SubtaskNode("s2", "Verify");

        module.AddChild(comp);
        comp.AddChild(task);
        task.AddChild(sub1);
        task.AddChild(sub2);

        var leaves = module.Leaves().ToList();

        Assert.Equal(2, leaves.Count);
        Assert.Contains(sub1, leaves);
        Assert.Contains(sub2, leaves);
    }

    [Fact]
    public void Leaves_ReturnsSelfForLeaf()
    {
        var subtask = new SubtaskNode("s1", "Parse");
        var leaves = subtask.Leaves().ToList();

        Assert.Single(leaves);
        Assert.Same(subtask, leaves[0]);
    }

    [Fact]
    public void ComputeAggregateState_AllPending_ReturnsPending()
    {
        var task = new TaskNode("t1", "Task");
        task.AddChild(new SubtaskNode("s1", "Sub1"));
        task.AddChild(new SubtaskNode("s2", "Sub2"));

        Assert.Equal(WorkNodeState.Pending, task.ComputeAggregateState());
    }

    [Fact]
    public void ComputeAggregateState_AllComplete_ReturnsComplete()
    {
        var task = new TaskNode("t1", "Task");
        var s1 = new SubtaskNode("s1", "Sub1");
        var s2 = new SubtaskNode("s2", "Sub2");
        task.AddChild(s1);
        task.AddChild(s2);

        s1.TransitionTo(WorkNodeState.InProgress);
        s1.TransitionTo(WorkNodeState.Complete);
        s2.TransitionTo(WorkNodeState.InProgress);
        s2.TransitionTo(WorkNodeState.Complete);

        Assert.Equal(WorkNodeState.Complete, task.ComputeAggregateState());
    }

    [Fact]
    public void ComputeAggregateState_AnyFailed_ReturnsFailed()
    {
        var task = new TaskNode("t1", "Task");
        var s1 = new SubtaskNode("s1", "Sub1");
        var s2 = new SubtaskNode("s2", "Sub2");
        task.AddChild(s1);
        task.AddChild(s2);

        s1.TransitionTo(WorkNodeState.InProgress);
        s1.TransitionTo(WorkNodeState.Complete);
        s2.TransitionTo(WorkNodeState.InProgress);
        s2.TransitionTo(WorkNodeState.Failed);

        Assert.Equal(WorkNodeState.Failed, task.ComputeAggregateState());
    }

    [Fact]
    public void ComputeAggregateState_MixedInProgress_ReturnsInProgress()
    {
        var task = new TaskNode("t1", "Task");
        var s1 = new SubtaskNode("s1", "Sub1");
        var s2 = new SubtaskNode("s2", "Sub2");
        task.AddChild(s1);
        task.AddChild(s2);

        s1.TransitionTo(WorkNodeState.InProgress);
        s1.TransitionTo(WorkNodeState.Complete);
        // s2 still pending

        Assert.Equal(WorkNodeState.InProgress, task.ComputeAggregateState());
    }

    [Fact]
    public void ComputeAggregateState_NoChildren_ReturnsOwnState()
    {
        var subtask = new SubtaskNode("s1", "Sub");
        Assert.Equal(WorkNodeState.Pending, subtask.ComputeAggregateState());

        subtask.TransitionTo(WorkNodeState.InProgress);
        Assert.Equal(WorkNodeState.InProgress, subtask.ComputeAggregateState());
    }
}
