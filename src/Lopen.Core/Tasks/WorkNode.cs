namespace Lopen.Core.Tasks;

/// <summary>
/// Generic composite base class for the task hierarchy.
/// Provides state management and child node tracking.
/// </summary>
/// <typeparam name="TChild">The type of child work nodes.</typeparam>
public abstract class WorkNode<TChild> : IWorkNode where TChild : IWorkNode
{
    private readonly List<TChild> _children = [];

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public WorkNodeState State { get; private set; }

    /// <inheritdoc />
    public IWorkNode? Parent { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<IWorkNode> Children => _children.Cast<IWorkNode>().ToList().AsReadOnly();

    /// <summary>
    /// The typed child nodes.
    /// </summary>
    public IReadOnlyList<TChild> TypedChildren => _children.AsReadOnly();

    /// <summary>
    /// Initializes a new work node.
    /// </summary>
    protected WorkNode(string id, string name)
    {
        Id = id;
        Name = name;
        State = WorkNodeState.Pending;
    }

    /// <inheritdoc />
    public void TransitionTo(WorkNodeState targetState)
    {
        ValidateTransition(State, targetState);
        State = targetState;
    }

    /// <summary>
    /// Adds a child node to this work node.
    /// </summary>
    public void AddChild(TChild child)
    {
        if (child is WorkNode<IWorkNode> workNode)
        {
            workNode.SetParent(this);
        }

        _children.Add(child);
    }

    internal void SetParent(IWorkNode parent)
    {
        Parent = parent;
    }

    private static void ValidateTransition(WorkNodeState current, WorkNodeState target)
    {
        var valid = (current, target) switch
        {
            (WorkNodeState.Pending, WorkNodeState.InProgress) => true,
            (WorkNodeState.InProgress, WorkNodeState.Complete) => true,
            (WorkNodeState.InProgress, WorkNodeState.Failed) => true,
            (WorkNodeState.Failed, WorkNodeState.InProgress) => true, // retry
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidStateTransitionException(current, target);
        }
    }
}
