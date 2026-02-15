namespace Lopen.Core.Workflow;

/// <summary>
/// Represents the current development state of a module.
/// </summary>
public enum ModuleStatus
{
    /// <summary>No acceptance criteria checkboxes are checked.</summary>
    NotStarted,

    /// <summary>Some but not all acceptance criteria checkboxes are checked.</summary>
    InProgress,

    /// <summary>All acceptance criteria checkboxes are checked.</summary>
    Complete,

    /// <summary>Module has no specification file.</summary>
    Unknown
}
