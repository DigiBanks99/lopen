namespace Lopen.Core.Tasks;

/// <summary>
/// Represents a node in the task hierarchy tree.
/// </summary>
public interface IWorkNode
{
    /// <summary>Unique identifier for the work node.</summary>
    string Id { get; }

    /// <summary>Display name of the work node.</summary>
    string Name { get; }

    /// <summary>Current state of the work node.</summary>
    WorkNodeState State { get; }

    /// <summary>The parent node, or null if this is the root.</summary>
    IWorkNode? Parent { get; }

    /// <summary>Child nodes.</summary>
    IReadOnlyList<IWorkNode> Children { get; }

    /// <summary>
    /// Transitions this node to the specified state.
    /// </summary>
    /// <param name="targetState">The state to transition to.</param>
    /// <exception cref="InvalidStateTransitionException">If the transition is invalid.</exception>
    void TransitionTo(WorkNodeState targetState);
}
