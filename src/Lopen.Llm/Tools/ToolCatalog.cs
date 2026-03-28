using Lopen.Storage;
using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tools;

/// <summary>
/// Assembles AIFunction instances per workflow phase using static operations classes.
/// Replaces the three-layer DefaultToolRegistry/ToolHandlerBinder/ToolConversion infrastructure.
/// </summary>
internal sealed class ToolCatalog
{
    private readonly IReadOnlyList<AIFunction> _specTools;
    private readonly IReadOnlyList<AIFunction> _researchTools;
    private readonly IReadOnlyList<AIFunction> _workflowTools;
    private readonly IReadOnlyList<AIFunction> _verificationTools;

    public ToolCatalog(
        IFileSystem fileSystem,
        IToolSectionExtractor sectionExtractor,
        IToolWorkflowEngine engine,
        IVerificationTracker verificationTracker,
        string projectRoot,
        ITaskStatusGate? taskStatusGate = null,
        IPlanManager? planManager = null,
        IOracleVerifier? oracleVerifier = null)
    {
        var specTools = new List<AIFunction>();
        SpecificationOperations.AddTools(specTools, fileSystem, sectionExtractor, projectRoot);
        _specTools = specTools;

        var researchTools = new List<AIFunction>();
        ResearchOperations.AddTools(researchTools, fileSystem, projectRoot);
        _researchTools = researchTools;

        var workflowTools = new List<AIFunction>();
        WorkflowOperations.AddTools(workflowTools, fileSystem, engine, taskStatusGate, verificationTracker, planManager, projectRoot);
        _workflowTools = workflowTools;

        var verificationTools = new List<AIFunction>();
        VerificationOperations.AddTools(verificationTools, oracleVerifier, verificationTracker);
        _verificationTools = verificationTools;
    }

    /// <summary>
    /// Returns the AIFunction instances appropriate for the given workflow phase.
    /// </summary>
    public IReadOnlyList<AIFunction> GetToolsForPhase(WorkflowPhase phase)
    {
        var tools = new List<AIFunction>();

        switch (phase)
        {
            case WorkflowPhase.RequirementGathering:
                tools.AddRange(_specTools);
                tools.Add(GetToolByName(_researchTools, "read_research"));
                tools.Add(GetToolByName(_workflowTools, "get_current_context"));
                tools.Add(GetToolByName(_researchTools, "log_research"));
                tools.Add(GetToolByName(_workflowTools, "report_progress"));
                break;

            case WorkflowPhase.Planning:
                tools.AddRange(_specTools);
                tools.Add(GetToolByName(_researchTools, "read_research"));
                tools.Add(GetToolByName(_workflowTools, "read_plan"));
                tools.Add(GetToolByName(_workflowTools, "get_current_context"));
                tools.Add(GetToolByName(_researchTools, "log_research"));
                tools.Add(GetToolByName(_workflowTools, "report_progress"));
                break;

            case WorkflowPhase.Building:
                tools.Add(GetToolByName(_workflowTools, "read_plan"));
                tools.Add(GetToolByName(_workflowTools, "update_task_status"));
                tools.Add(GetToolByName(_workflowTools, "get_current_context"));
                tools.Add(GetToolByName(_workflowTools, "report_progress"));
                tools.AddRange(_verificationTools);
                break;

            case WorkflowPhase.Research:
                tools.AddRange(_specTools);
                tools.Add(GetToolByName(_researchTools, "read_research"));
                tools.Add(GetToolByName(_workflowTools, "get_current_context"));
                tools.Add(GetToolByName(_researchTools, "log_research"));
                tools.Add(GetToolByName(_workflowTools, "report_progress"));
                break;
        }

        return tools.AsReadOnly();
    }

    private static AIFunction GetToolByName(IReadOnlyList<AIFunction> tools, string name)
    {
        return tools.First(t => t.Name == name);
    }
}
