namespace Lopen.Core.Workflow;

/// <summary>
/// The 7 steps of the Lopen development workflow.
/// </summary>
public enum WorkflowStep
{
    /// <summary>Step 1: Conduct guided conversation to draft or refine a module specification.</summary>
    DraftSpecification,

    /// <summary>Step 2: Identify libraries, APIs, and other modules needed.</summary>
    DetermineDependencies,

    /// <summary>Step 3: Break the module into logical components and assess codebase state.</summary>
    IdentifyComponents,

    /// <summary>Step 4: Choose the next component based on dependency order and progress.</summary>
    SelectNextComponent,

    /// <summary>Step 5: Decompose the selected component into atomic tasks.</summary>
    BreakIntoTasks,

    /// <summary>Step 6: Iteratively complete tasks with self-correction and back-pressure.</summary>
    IterateThroughTasks,

    /// <summary>Step 7: Return to step 4 until all components are complete.</summary>
    Repeat,
}
