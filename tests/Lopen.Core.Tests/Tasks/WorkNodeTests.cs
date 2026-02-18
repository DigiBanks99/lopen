using Lopen.Core.Tasks;

namespace Lopen.Core.Tests.Tasks;

public class WorkNodeTests
{
    [Fact]
    public void NewNode_HasPendingState()
    {
        var node = new TaskNode("t1", "Task 1");
        Assert.Equal(WorkNodeState.Pending, node.State);
    }

    [Fact]
    public void NewNode_HasNoParent()
    {
        var node = new TaskNode("t1", "Task 1");
        Assert.Null(node.Parent);
    }

    [Fact]
    public void NewNode_HasNoChildren()
    {
        var node = new TaskNode("t1", "Task 1");
        Assert.Empty(node.Children);
    }

    [Fact]
    public void TransitionTo_Pending_To_InProgress_Succeeds()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        Assert.Equal(WorkNodeState.InProgress, node.State);
    }

    [Fact]
    public void TransitionTo_InProgress_To_Complete_Succeeds()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Complete);
        Assert.Equal(WorkNodeState.Complete, node.State);
    }

    [Fact]
    public void TransitionTo_InProgress_To_Failed_Succeeds()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Failed);
        Assert.Equal(WorkNodeState.Failed, node.State);
    }

    [Fact]
    public void TransitionTo_Failed_To_InProgress_Succeeds()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Failed);
        node.TransitionTo(WorkNodeState.InProgress);
        Assert.Equal(WorkNodeState.InProgress, node.State);
    }

    [Fact]
    public void TransitionTo_Pending_To_Complete_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Complete));
    }

    [Fact]
    public void TransitionTo_Pending_To_Failed_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Failed));
    }

    [Fact]
    public void TransitionTo_Complete_To_InProgress_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Complete);
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.InProgress));
    }

    [Fact]
    public void TransitionTo_Complete_To_Failed_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Complete);
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Failed));
    }

    [Fact]
    public void TransitionTo_Complete_To_Pending_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Complete);
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Pending));
    }

    [Fact]
    public void TransitionTo_Failed_To_Complete_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        node.TransitionTo(WorkNodeState.Failed);
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Complete));
    }

    [Fact]
    public void TransitionTo_InProgress_To_Pending_Throws()
    {
        var node = new TaskNode("t1", "Task 1");
        node.TransitionTo(WorkNodeState.InProgress);
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Pending));
    }

    [Fact]
    public void AddChild_AddsToChildren()
    {
        var task = new TaskNode("t1", "Task 1");
        var subtask = new SubtaskNode("s1", "Subtask 1");
        task.AddChild(subtask);

        Assert.Single(task.Children);
        Assert.Single(task.TypedChildren);
        Assert.Same(subtask, task.TypedChildren[0]);
    }

    [Fact]
    public void AddChild_MultipleChildren()
    {
        var task = new TaskNode("t1", "Task 1");
        task.AddChild(new SubtaskNode("s1", "Sub 1"));
        task.AddChild(new SubtaskNode("s2", "Sub 2"));
        task.AddChild(new SubtaskNode("s3", "Sub 3"));

        Assert.Equal(3, task.Children.Count);
        Assert.Equal(3, task.TypedChildren.Count);
    }

    [Fact]
    public void Id_ReturnsConstructorValue()
    {
        var node = new TaskNode("task-123", "My Task");
        Assert.Equal("task-123", node.Id);
    }

    [Fact]
    public void Name_ReturnsConstructorValue()
    {
        var node = new TaskNode("t1", "My Task");
        Assert.Equal("My Task", node.Name);
    }
}
