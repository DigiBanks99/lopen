namespace Lopen.Storage;

/// <summary>
/// Trigger types for auto-save.
/// </summary>
public enum AutoSaveTrigger
{
    /// <summary>A workflow step completed.</summary>
    StepCompletion,

    /// <summary>A task completed successfully.</summary>
    TaskCompletion,

    /// <summary>A task failed.</summary>
    TaskFailure,

    /// <summary>A workflow phase transition occurred.</summary>
    PhaseTransition,

    /// <summary>A component completed.</summary>
    ComponentCompletion,

    /// <summary>User-initiated pause or context switch.</summary>
    UserPause,
}
