using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Manages Lopen-managed tool definitions and provides phase-appropriate tool sets.
/// Pre-registers the 10 built-in tools with their phase availability.
/// </summary>
internal sealed class DefaultToolRegistry : IToolRegistry
{
    private readonly List<LopenToolDefinition> _tools = [];
    private readonly ILogger<DefaultToolRegistry> _logger;

    public DefaultToolRegistry(ILogger<DefaultToolRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RegisterBuiltInTools();
    }

    public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase)
    {
        return _tools
            .Where(t => t.AvailableInPhases is null || t.AvailableInPhases.Contains(phase))
            .ToList()
            .AsReadOnly();
    }

    public void RegisterTool(LopenToolDefinition tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_tools.Any(t => t.Name == tool.Name))
        {
            _logger.LogWarning("Tool '{ToolName}' is already registered; skipping duplicate", tool.Name);
            return;
        }

        _tools.Add(tool);
    }

    public IReadOnlyList<LopenToolDefinition> GetAllTools() => _tools.AsReadOnly();

    public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(handler);

        var index = _tools.FindIndex(t => t.Name == toolName);
        if (index < 0)
        {
            _logger.LogWarning("Cannot bind handler: tool '{ToolName}' not found", toolName);
            return false;
        }

        _tools[index] = _tools[index] with { Handler = handler };
        _logger.LogDebug("Bound handler to tool '{ToolName}'", toolName);
        return true;
    }

    private void RegisterBuiltInTools()
    {
        var allPhases = new[]
        {
            WorkflowPhase.RequirementGathering,
            WorkflowPhase.Planning,
            WorkflowPhase.Building,
            WorkflowPhase.Research,
        };

        // Orchestration tools (7)
        RegisterTool(new LopenToolDefinition(
            "read_spec",
            "Read a specific section from a specification document",
            AvailableInPhases: allPhases));

        RegisterTool(new LopenToolDefinition(
            "read_research",
            "Read findings from a research document",
            AvailableInPhases: allPhases));

        RegisterTool(new LopenToolDefinition(
            "read_plan",
            "Read the current plan with task statuses",
            AvailableInPhases: [WorkflowPhase.Planning, WorkflowPhase.Building]));

        RegisterTool(new LopenToolDefinition(
            "update_task_status",
            "Mark a task as pending, in-progress, complete, or failed",
            AvailableInPhases: [WorkflowPhase.Building]));

        RegisterTool(new LopenToolDefinition(
            "get_current_context",
            "Retrieve the current workflow step, module, component, and task",
            AvailableInPhases: allPhases));

        RegisterTool(new LopenToolDefinition(
            "log_research",
            "Save research findings to docs/requirements/{module}/RESEARCH-{topic}.md",
            AvailableInPhases: [WorkflowPhase.Research, WorkflowPhase.RequirementGathering]));

        RegisterTool(new LopenToolDefinition(
            "report_progress",
            "Report what was accomplished in this iteration",
            AvailableInPhases: allPhases));

        // Verification tools (3) — only during Building
        RegisterTool(new LopenToolDefinition(
            "verify_task_completion",
            "Dispatch oracle sub-agent to verify a task is complete",
            AvailableInPhases: [WorkflowPhase.Building]));

        RegisterTool(new LopenToolDefinition(
            "verify_component_completion",
            "Dispatch oracle sub-agent to verify all tasks in a component are complete",
            AvailableInPhases: [WorkflowPhase.Building]));

        RegisterTool(new LopenToolDefinition(
            "verify_module_completion",
            "Dispatch oracle sub-agent to verify the module meets all acceptance criteria",
            AvailableInPhases: [WorkflowPhase.Building]));
    }
}
