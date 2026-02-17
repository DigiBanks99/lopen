namespace Lopen.Core.Tasks;

/// <summary>
/// Leaf subtask node in the task hierarchy.
/// </summary>
public sealed class SubtaskNode : IWorkNode
{
    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public WorkNodeState State { get; private set; }

    /// <inheritdoc />
    public IWorkNode? Parent { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyList<IWorkNode> Children => [];

    /// <summary>
    /// Initializes a new subtask node.
    /// </summary>
    public SubtaskNode(string id, string name)
    {
        Id = id;
        Name = name;
        State = WorkNodeState.Pending;
    }

    /// <inheritdoc />
    public void TransitionTo(WorkNodeState targetState)
    {
        var valid = (State, targetState) switch
        {
            (WorkNodeState.Pending, WorkNodeState.InProgress) => true,
            (WorkNodeState.InProgress, WorkNodeState.Complete) => true,
            (WorkNodeState.InProgress, WorkNodeState.Failed) => true,
            (WorkNodeState.Failed, WorkNodeState.InProgress) => true,
            _ => false,
        };

        if (!valid)
        {
            throw new InvalidStateTransitionException(State, targetState);
        }

        State = targetState;
    }
}
