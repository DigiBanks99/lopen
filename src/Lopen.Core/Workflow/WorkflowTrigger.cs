namespace Lopen.Core.Workflow;

/// <summary>
/// Triggers that cause transitions between workflow steps in the state machine.
/// </summary>
public enum WorkflowTrigger
{
    /// <summary>Assess the current codebase state and determine the correct step.</summary>
    Assess,

    /// <summary>User approves the specification (human gate).</summary>
    SpecApproved,

    /// <summary>Dependencies have been determined.</summary>
    DependenciesDetermined,

    /// <summary>Components have been identified and assessed.</summary>
    ComponentsIdentified,

    /// <summary>A component has been selected for building.</summary>
    ComponentSelected,

    /// <summary>Tasks have been broken down for the selected component.</summary>
    TasksBrokenDown,

    /// <summary>A task iteration has completed (success or failure).</summary>
    TaskIterationComplete,

    /// <summary>All tasks for the current component are complete.</summary>
    ComponentComplete,

    /// <summary>All components and acceptance criteria are satisfied.</summary>
    ModuleComplete,
}
