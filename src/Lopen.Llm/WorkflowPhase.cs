namespace Lopen.Llm;

/// <summary>
/// Workflow phases that map to different model selection and tool registration.
/// </summary>
public enum WorkflowPhase
{
    /// <summary>Step 1: Draft specification from user requirements.</summary>
    RequirementGathering,

    /// <summary>Steps 2–5: Dependencies, components, selection, task breakdown.</summary>
    Planning,

    /// <summary>Step 6: Per-task code implementation.</summary>
    Building,

    /// <summary>Standalone research invocations.</summary>
    Research,
}
