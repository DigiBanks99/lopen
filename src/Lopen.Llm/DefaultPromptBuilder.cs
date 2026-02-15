using System.Text;
using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Assembles structured system prompts for SDK invocations.
/// Each prompt contains labeled sections for role, state, instructions, context, tools, and constraints.
/// </summary>
internal sealed class DefaultPromptBuilder : IPromptBuilder
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<DefaultPromptBuilder> _logger;

    public DefaultPromptBuilder(IToolRegistry toolRegistry, ILogger<DefaultPromptBuilder> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string BuildSystemPrompt(
        WorkflowPhase phase,
        string module,
        string? component,
        string? task,
        IReadOnlyDictionary<string, string>? contextSections = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var sb = new StringBuilder();

        AppendRoleSection(sb);
        AppendWorkflowStateSection(sb, phase, module, component, task);
        AppendInstructionsSection(sb, phase);
        AppendContextSections(sb, contextSections);
        AppendToolsSection(sb, phase);
        AppendConstraintsSection(sb);

        return sb.ToString();
    }

    private static void AppendRoleSection(StringBuilder sb)
    {
        sb.AppendLine("# Role");
        sb.AppendLine();
        sb.AppendLine("You are working within Lopen, an orchestrator for module development. " +
            "You implement features by following a structured workflow, using Lopen-managed tools " +
            "for state management and native tools for implementation work.");
        sb.AppendLine();
    }

    private static void AppendWorkflowStateSection(
        StringBuilder sb, WorkflowPhase phase, string module, string? component, string? task)
    {
        sb.AppendLine("# Workflow State");
        sb.AppendLine();
        sb.AppendLine($"- **Phase**: {phase}");
        sb.AppendLine($"- **Module**: {module}");

        if (!string.IsNullOrWhiteSpace(component))
            sb.AppendLine($"- **Component**: {component}");

        if (!string.IsNullOrWhiteSpace(task))
            sb.AppendLine($"- **Task**: {task}");

        sb.AppendLine();
    }

    private static void AppendInstructionsSection(StringBuilder sb, WorkflowPhase phase)
    {
        sb.AppendLine("# Instructions");
        sb.AppendLine();

        var instructions = phase switch
        {
            WorkflowPhase.RequirementGathering =>
                "Gather and refine requirements for this module. Read the specification, " +
                "identify gaps, and produce a clear, complete spec. Use `read_spec` and " +
                "`log_research` to capture findings.",
            WorkflowPhase.Planning =>
                "Plan the implementation for this module. Analyze dependencies, define components, " +
                "break work into tasks, and select the next component to build. Use `read_spec` " +
                "and `read_plan` to inform decisions.",
            WorkflowPhase.Building =>
                "Implement the current task. Write code, tests, and documentation. Use native tools " +
                "for file operations and shell commands. When complete, call `verify_task_completion` " +
                "before marking the task as done with `update_task_status`.",
            WorkflowPhase.Research =>
                "Research the topic to gather information needed for implementation. " +
                "Use `log_research` to save findings for future reference.",
            _ => "Follow the workflow instructions for the current phase.",
        };

        sb.AppendLine(instructions);
        sb.AppendLine();
    }

    private static void AppendContextSections(
        StringBuilder sb, IReadOnlyDictionary<string, string>? contextSections)
    {
        if (contextSections is null || contextSections.Count == 0)
            return;

        sb.AppendLine("# Context");
        sb.AppendLine();

        foreach (var (title, content) in contextSections)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            sb.AppendLine(content);
            sb.AppendLine();
        }
    }

    private void AppendToolsSection(StringBuilder sb, WorkflowPhase phase)
    {
        var tools = _toolRegistry.GetToolsForPhase(phase);

        sb.AppendLine("# Available Tools");
        sb.AppendLine();

        if (tools.Count == 0)
        {
            sb.AppendLine("No Lopen-managed tools available for this phase.");
        }
        else
        {
            foreach (var tool in tools)
            {
                sb.AppendLine($"- **{tool.Name}**: {tool.Description}");
            }
        }

        sb.AppendLine();
    }

    private static void AppendConstraintsSection(StringBuilder sb)
    {
        sb.AppendLine("# Constraints");
        sb.AppendLine();
        sb.AppendLine("- Use conventional commit messages for all commits");
        sb.AppendLine("- Write tests for new functionality before marking tasks complete");
        sb.AppendLine("- Do not modify files outside the current module scope without justification");
        sb.AppendLine("- Call verification tools before marking work as complete");
    }
}
