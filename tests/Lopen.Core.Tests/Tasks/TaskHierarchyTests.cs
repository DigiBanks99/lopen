using Lopen.Core.Tasks;

namespace Lopen.Core.Tests.Tasks;

public class TaskHierarchyTests
{
    [Fact]
    public void ModuleNode_CanContainComponents()
    {
        var module = new ModuleNode("mod1", "Auth Module");
        var component = new ComponentNode("comp1", "JWT Component");
        module.AddChild(component);

        Assert.Single(module.TypedChildren);
        Assert.Same(component, module.TypedChildren[0]);
    }

    [Fact]
    public void ComponentNode_CanContainTasks()
    {
        var component = new ComponentNode("comp1", "JWT Component");
        var task = new TaskNode("t1", "Implement validation");
        component.AddChild(task);

        Assert.Single(component.TypedChildren);
        Assert.Same(task, component.TypedChildren[0]);
    }

    [Fact]
    public void TaskNode_CanContainSubtasks()
    {
        var task = new TaskNode("t1", "Implement validation");
        var subtask = new SubtaskNode("s1", "Parse token");
        task.AddChild(subtask);

        Assert.Single(task.TypedChildren);
        Assert.Same(subtask, task.TypedChildren[0]);
    }

    [Fact]
    public void SubtaskNode_IsLeaf()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        Assert.Empty(subtask.Children);
    }

    [Fact]
    public void SubtaskNode_StartsAsPending()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        Assert.Equal(WorkNodeState.Pending, subtask.State);
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Pending_To_InProgress_Succeeds()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        Assert.Equal(WorkNodeState.InProgress, subtask.State);
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Invalid_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.Complete));
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Complete_To_Failed_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Complete);
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.Failed));
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Complete_To_Pending_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Complete);
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.Pending));
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Failed_To_Complete_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Failed);
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.Complete));
    }

    [Fact]
    public void SubtaskNode_TransitionTo_InProgress_To_Pending_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.Pending));
    }

    [Fact]
    public void SubtaskNode_TransitionTo_InProgress_To_Complete_Succeeds()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Complete);
        Assert.Equal(WorkNodeState.Complete, subtask.State);
    }

    [Fact]
    public void SubtaskNode_TransitionTo_InProgress_To_Failed_Succeeds()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Failed);
        Assert.Equal(WorkNodeState.Failed, subtask.State);
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Failed_To_InProgress_Succeeds()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Failed);
        subtask.TransitionTo(WorkNodeState.InProgress);
        Assert.Equal(WorkNodeState.InProgress, subtask.State);
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Pending_To_Failed_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.Failed));
    }

    [Fact]
    public void SubtaskNode_TransitionTo_Complete_To_InProgress_Throws()
    {
        var subtask = new SubtaskNode("s1", "Parse token");
        subtask.TransitionTo(WorkNodeState.InProgress);
        subtask.TransitionTo(WorkNodeState.Complete);
        Assert.Throws<InvalidStateTransitionException>(() =>
            subtask.TransitionTo(WorkNodeState.InProgress));
    }

    [Fact]
    public void FullHierarchy_FourLevels()
    {
        var module = new ModuleNode("mod1", "Auth");
        var component = new ComponentNode("comp1", "JWT");
        var task = new TaskNode("t1", "Validate");
        var subtask = new SubtaskNode("s1", "Parse");

        module.AddChild(component);
        component.AddChild(task);
        task.AddChild(subtask);

        Assert.Single(module.Children);
        Assert.Single(component.Children);
        Assert.Single(task.Children);
        Assert.Empty(subtask.Children);
    }
}
