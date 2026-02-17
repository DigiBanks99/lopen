namespace Lopen.Core.Tasks;

/// <summary>
/// Task-level node in the task hierarchy.
/// </summary>
public sealed class TaskNode : WorkNode<SubtaskNode>
{
    /// <summary>
    /// Initializes a new task node.
    /// </summary>
    public TaskNode(string id, string name) : base(id, name) { }
}
