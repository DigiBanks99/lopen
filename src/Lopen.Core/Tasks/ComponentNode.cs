namespace Lopen.Core.Tasks;

/// <summary>
/// Component-level node in the task hierarchy.
/// </summary>
public sealed class ComponentNode : WorkNode<TaskNode>
{
    /// <summary>
    /// Initializes a new component node.
    /// </summary>
    public ComponentNode(string id, string name) : base(id, name) { }
}
