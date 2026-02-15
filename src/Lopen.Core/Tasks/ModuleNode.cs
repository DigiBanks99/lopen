namespace Lopen.Core.Tasks;

/// <summary>
/// Top-level module node in the task hierarchy.
/// </summary>
public sealed class ModuleNode : WorkNode<ComponentNode>
{
    /// <summary>
    /// Initializes a new module node.
    /// </summary>
    public ModuleNode(string id, string name) : base(id, name) { }
}
