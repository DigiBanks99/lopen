namespace Lopen.Llm.Tools;

/// <summary>
/// Adapter interface for workflow engine queries, avoiding a circular Lopen.Core → Lopen.Llm dependency.
/// Lopen.Core provides the concrete adapter bridging IWorkflowEngine to this interface.
/// </summary>
public interface IToolWorkflowEngine
{
    /// <summary>Gets the current workflow step name.</summary>
    string CurrentStep { get; }

    /// <summary>Maps the current step to its workflow phase.</summary>
    WorkflowPhase CurrentPhase { get; }

    /// <summary>Returns true if the workflow has reached completion.</summary>
    bool IsComplete { get; }

    /// <summary>Returns the names of triggers that are currently permitted.</summary>
    IReadOnlyList<string> GetPermittedTriggers();
}
