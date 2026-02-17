namespace Lopen.Storage;

/// <summary>
/// Serializable DTO representing a node in the task hierarchy tree.
/// Used to persist the full module → component → task → subtask structure in state.json.
/// </summary>
public sealed record TaskHierarchyNode
{
    /// <summary>Unique identifier for the work node.</summary>
    public required string Id { get; init; }

    /// <summary>Display name of the work node.</summary>
    public required string Name { get; init; }

    /// <summary>Current state of the work node (Pending, InProgress, Complete, Failed).</summary>
    public required string State { get; init; }

    /// <summary>The type of node in the hierarchy (module, component, task, subtask).</summary>
    public required string NodeType { get; init; }

    /// <summary>Child nodes in the hierarchy.</summary>
    public IReadOnlyList<TaskHierarchyNode> Children { get; init; } = [];
}
