namespace Lopen.Core.Tasks;

/// <summary>
/// Extension methods for traversing and querying the task hierarchy.
/// </summary>
public static class WorkNodeExtensions
{
    /// <summary>
    /// Returns all descendants of this node (depth-first traversal).
    /// </summary>
    public static IEnumerable<IWorkNode> Descendants(this IWorkNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Computes the aggregate state of a node based on its children.
    /// Returns <see cref="WorkNodeState.Complete"/> if all children are complete,
    /// <see cref="WorkNodeState.Failed"/> if any child is failed,
    /// <see cref="WorkNodeState.InProgress"/> if any child is in progress or a mix exists,
    /// <see cref="WorkNodeState.Pending"/> if all children are pending.
    /// </summary>
    public static WorkNodeState ComputeAggregateState(this IWorkNode node)
    {
        var children = node.Children;
        if (children.Count == 0)
        {
            return node.State;
        }

        if (children.All(c => c.State == WorkNodeState.Complete))
        {
            return WorkNodeState.Complete;
        }

        if (children.Any(c => c.State == WorkNodeState.Failed))
        {
            return WorkNodeState.Failed;
        }

        if (children.Any(c => c.State is WorkNodeState.InProgress or WorkNodeState.Complete))
        {
            return WorkNodeState.InProgress;
        }

        return WorkNodeState.Pending;
    }

    /// <summary>
    /// Returns all leaf nodes (nodes with no children) in the hierarchy.
    /// </summary>
    public static IEnumerable<IWorkNode> Leaves(this IWorkNode node)
    {
        if (node.Children.Count == 0)
        {
            yield return node;
            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var leaf in child.Leaves())
            {
                yield return leaf;
            }
        }
    }
}
