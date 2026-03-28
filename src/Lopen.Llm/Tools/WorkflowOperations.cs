using System.ComponentModel;
using System.Text.Json;
using Lopen.Storage;
using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tools;

/// <summary>
/// Static operations for workflow coordination tools.
/// </summary>
internal static class WorkflowOperations
{
    public static void AddTools(
        List<AIFunction> tools,
        IFileSystem fileSystem,
        IToolWorkflowEngine engine,
        ITaskStatusGate? taskStatusGate,
        IVerificationTracker verificationTracker,
        IPlanManager? planManager,
        string projectRoot)
    {
        tools.Add(AIFunctionFactory.Create(
            [Description("Read the current plan with task statuses")]
            (string? _unused) => ReadPlan(fileSystem, projectRoot),
            "read_plan"));

        tools.Add(AIFunctionFactory.Create(
            [Description("Mark a task as pending, in-progress, complete, or failed")]
            (string task_id, string status, string? module, string? component) =>
                UpdateTaskStatus(taskStatusGate, verificationTracker, planManager, task_id, status, module, component),
            "update_task_status"));

        tools.Add(AIFunctionFactory.Create(
            [Description("Retrieve the current workflow step, module, component, and task")]
            (string? _unused) => GetCurrentContext(engine),
            "get_current_context"));

        tools.Add(AIFunctionFactory.Create(
            [Description("Report what was accomplished in this iteration")]
            (string summary) => ReportProgress(summary),
            "report_progress"));
    }

    internal static async Task<string> ReadPlan(IFileSystem fileSystem, string projectRoot)
    {
        var planPath = Path.Combine(projectRoot, "docs", "requirements", "IMPLEMENTATION_PLAN.md");
        if (!fileSystem.FileExists(planPath))
            return JsonResult("error", "No implementation plan found");

        return await fileSystem.ReadAllTextAsync(planPath);
    }

    internal static async Task<string> UpdateTaskStatus(
        ITaskStatusGate? taskStatusGate,
        IVerificationTracker verificationTracker,
        IPlanManager? planManager,
        string taskId,
        string status,
        string? module,
        string? component)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(status))
            return JsonResult("error", "task_id and status are required");

        if (status.Equals("complete", StringComparison.OrdinalIgnoreCase))
        {
            if (taskStatusGate is not null)
            {
                TaskStatusGateResult gateResult = taskStatusGate.ValidateCompletion(VerificationScope.Task, taskId);
                if (!gateResult.IsAllowed)
                    return JsonResult("error", gateResult.RejectionReason ?? $"Cannot mark task '{taskId}' as complete");
            }
            else if (!verificationTracker.IsVerified(VerificationScope.Task, taskId))
            {
                return JsonResult("error",
                    $"Cannot mark task '{taskId}' as complete — verify_task_completion must pass first");
            }

            if (planManager is not null && !string.IsNullOrWhiteSpace(module))
                await planManager.UpdateCheckboxAsync(module, taskId, true);
        }

        return JsonResult("success", $"Task '{taskId}' status updated to '{status}'");
    }

    internal static Task<string> GetCurrentContext(IToolWorkflowEngine engine)
    {
        var context = new Dictionary<string, string>
        {
            ["step"] = engine.CurrentStep,
            ["phase"] = engine.CurrentPhase.ToString(),
            ["is_complete"] = engine.IsComplete.ToString(),
        };

        IReadOnlyList<string> permitted = engine.GetPermittedTriggers();
        context["permitted_triggers"] = string.Join(", ", permitted);

        return Task.FromResult(JsonSerializer.Serialize(context));
    }

    internal static Task<string> ReportProgress(string summary)
    {
        return Task.FromResult(JsonResult("success", $"Progress recorded: {summary}"));
    }

    internal static string JsonResult(string status, string message) =>
        JsonSerializer.Serialize(new { status, message });
}
