using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Llm.Tools;

namespace Lopen.Core;

/// <summary>
/// Adapts <see cref="IWorkflowEngine"/> to <see cref="IToolWorkflowEngine"/>
/// so ToolCatalog (in Lopen.Llm) can query workflow state without referencing Lopen.Core.
/// </summary>
internal sealed class WorkflowEngineToolAdapter : IToolWorkflowEngine
{
    private readonly IWorkflowEngine _inner;

    public WorkflowEngineToolAdapter(IWorkflowEngine inner) => _inner = inner;

    public string CurrentStep => _inner.CurrentStep.ToString();

    public WorkflowPhase CurrentPhase => _inner.CurrentPhase;

    public bool IsComplete => _inner.IsComplete;

    public IReadOnlyList<string> GetPermittedTriggers() =>
        _inner.GetPermittedTriggers().Select(t => t.ToString()).ToList();
}
