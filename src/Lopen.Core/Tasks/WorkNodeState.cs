namespace Lopen.Core.Tasks;

/// <summary>
/// The state of a work node in the task hierarchy.
/// </summary>
public enum WorkNodeState
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>Currently being worked on.</summary>
    InProgress,

    /// <summary>Successfully completed.</summary>
    Complete,

    /// <summary>Failed and blocked.</summary>
    Failed,
}
