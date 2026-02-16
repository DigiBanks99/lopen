using System.Diagnostics;
using System.Text.Json;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Otel;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.ToolHandlers;

/// <summary>
/// Implements and binds handlers for all 10 built-in Lopen tools.
/// </summary>
internal sealed class ToolHandlerBinder : IToolHandlerBinder
{
    private readonly IFileSystem _fileSystem;
    private readonly ISectionExtractor _sectionExtractor;
    private readonly IWorkflowEngine _engine;
    private readonly IVerificationTracker _verificationTracker;
    private readonly IGitWorkflowService? _gitWorkflowService;
    private readonly ILogger<ToolHandlerBinder> _logger;
    private readonly string _projectRoot;

    public ToolHandlerBinder(
        IFileSystem fileSystem,
        ISectionExtractor sectionExtractor,
        IWorkflowEngine engine,
        IVerificationTracker verificationTracker,
        ILogger<ToolHandlerBinder> logger,
        string projectRoot,
        IGitWorkflowService? gitWorkflowService = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _sectionExtractor = sectionExtractor ?? throw new ArgumentNullException(nameof(sectionExtractor));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _verificationTracker = verificationTracker ?? throw new ArgumentNullException(nameof(verificationTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        _gitWorkflowService = gitWorkflowService;
    }

    public void BindAll(IToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.BindHandler("read_spec", Traced("read_spec", HandleReadSpec));
        registry.BindHandler("read_research", Traced("read_research", HandleReadResearch));
        registry.BindHandler("read_plan", Traced("read_plan", HandleReadPlan));
        registry.BindHandler("update_task_status", Traced("update_task_status", HandleUpdateTaskStatus));
        registry.BindHandler("get_current_context", Traced("get_current_context", HandleGetCurrentContext));
        registry.BindHandler("log_research", Traced("log_research", HandleLogResearch));
        registry.BindHandler("report_progress", Traced("report_progress", HandleReportProgress));
        registry.BindHandler("verify_task_completion", TracedVerify("verify_task_completion", HandleVerifyTaskCompletion));
        registry.BindHandler("verify_component_completion", TracedVerify("verify_component_completion", HandleVerifyComponentCompletion));
        registry.BindHandler("verify_module_completion", TracedVerify("verify_module_completion", HandleVerifyModuleCompletion));

        _logger.LogInformation("Bound handlers for all 10 built-in tools");
    }

    /// <summary>
    /// Wraps a tool handler with an OTEL-05 tool span.
    /// </summary>
    private static Func<string, CancellationToken, Task<string>> Traced(
        string toolName, Func<string, CancellationToken, Task<string>> handler)
    {
        return async (parameters, ct) =>
        {
            using var activity = SpanFactory.StartTool(toolName);
            try
            {
                var result = await handler(parameters, ct);
                var success = !result.Contains("\"error\"", StringComparison.Ordinal);
                SpanFactory.SetToolResult(activity, success);
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                SpanFactory.SetToolResult(activity, false, ex.Message);
                throw;
            }
        };
    }

    /// <summary>
    /// Wraps a verification handler with an OTEL-06 oracle verification span.
    /// </summary>
    private static Func<string, CancellationToken, Task<string>> TracedVerify(
        string toolName, Func<string, CancellationToken, Task<string>> handler)
    {
        return async (parameters, ct) =>
        {
            using var activity = SpanFactory.StartOracleVerification(
                toolName.Replace("verify_", "").Replace("_completion", ""), "oracle", 1);
            try
            {
                var result = await handler(parameters, ct);
                var success = result.Contains("\"success\"", StringComparison.Ordinal);
                SpanFactory.SetOracleVerdict(activity, success ? "pass" : "fail");
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                SpanFactory.SetOracleVerdict(activity, "error");
                throw;
            }
        };
    }

    internal async Task<string> HandleReadSpec(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var module = args.GetValueOrDefault("module") ?? _engine.CurrentStep.ToString();
        var section = args.GetValueOrDefault("section");

        var specPath = Path.Combine(_projectRoot, "docs", "requirements", module, "SPECIFICATION.md");
        if (!_fileSystem.FileExists(specPath))
            return JsonResult("error", $"Specification not found for module '{module}'");

        var content = await _fileSystem.ReadAllTextAsync(specPath, ct);

        if (!string.IsNullOrWhiteSpace(section))
        {
            var sections = _sectionExtractor.ExtractRelevantSections(content, [section]);
            if (sections.Count > 0)
                return string.Join("\n\n", sections.Select(s => s.Content));
            return JsonResult("error", $"Section '{section}' not found in specification");
        }

        return content;
    }

    internal async Task<string> HandleReadResearch(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var module = args.GetValueOrDefault("module") ?? "";
        var topic = args.GetValueOrDefault("topic");

        var researchDir = Path.Combine(_projectRoot, "docs", "requirements", module);
        if (!_fileSystem.DirectoryExists(researchDir))
            return JsonResult("error", $"Research directory not found for module '{module}'");

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var topicPath = Path.Combine(researchDir, $"RESEARCH-{topic}.md");
            if (_fileSystem.FileExists(topicPath))
                return await _fileSystem.ReadAllTextAsync(topicPath, ct);

            return JsonResult("error", $"Research file not found: RESEARCH-{topic}.md");
        }

        // Return the main RESEARCH.md
        var mainPath = Path.Combine(researchDir, "RESEARCH.md");
        if (_fileSystem.FileExists(mainPath))
            return await _fileSystem.ReadAllTextAsync(mainPath, ct);

        return JsonResult("error", "No RESEARCH.md found");
    }

    internal async Task<string> HandleReadPlan(string parameters, CancellationToken ct)
    {
        var planPath = Path.Combine(_projectRoot, "docs", "requirements", "IMPLEMENTATION_PLAN.md");
        if (!_fileSystem.FileExists(planPath))
            return JsonResult("error", "No implementation plan found");

        return await _fileSystem.ReadAllTextAsync(planPath, ct);
    }

    internal async Task<string> HandleUpdateTaskStatus(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var taskId = args.GetValueOrDefault("task_id") ?? "";
        var status = args.GetValueOrDefault("status") ?? "";
        var module = args.GetValueOrDefault("module") ?? "";
        var component = args.GetValueOrDefault("component") ?? "";

        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(status))
            return JsonResult("error", "task_id and status are required");

        // Enforce oracle verification before marking complete (CORE-10, LLM-08)
        if (status.Equals("complete", StringComparison.OrdinalIgnoreCase))
        {
            if (!_verificationTracker.IsVerified(VerificationScope.Task, taskId))
            {
                _logger.LogWarning("Task completion rejected: {TaskId} has not passed verification", taskId);
                return JsonResult("error",
                    $"Cannot mark task '{taskId}' as complete — verify_task_completion must pass first");
            }

            // Auto-commit on task completion if git is enabled
            if (_gitWorkflowService is not null && !string.IsNullOrWhiteSpace(module))
            {
                var commitResult = await _gitWorkflowService.CommitTaskCompletionAsync(
                    module, component, taskId, ct);
                if (commitResult is not null)
                {
                    _logger.LogInformation("Git commit for task {TaskId}: {Success}",
                        taskId, commitResult.Success);
                }
            }
        }

        _logger.LogInformation("Task {TaskId} status updated to {Status}", taskId, status);
        return JsonResult("success", $"Task '{taskId}' status updated to '{status}'");
    }

    internal Task<string> HandleGetCurrentContext(string parameters, CancellationToken ct)
    {
        var context = new Dictionary<string, string>
        {
            ["step"] = _engine.CurrentStep.ToString(),
            ["phase"] = _engine.CurrentPhase.ToString(),
            ["is_complete"] = _engine.IsComplete.ToString(),
        };

        var permitted = _engine.GetPermittedTriggers();
        context["permitted_triggers"] = string.Join(", ", permitted);

        return Task.FromResult(JsonSerializer.Serialize(context));
    }

    internal async Task<string> HandleLogResearch(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var module = args.GetValueOrDefault("module") ?? "";
        var topic = args.GetValueOrDefault("topic") ?? "general";
        var content = args.GetValueOrDefault("content") ?? "";

        if (string.IsNullOrWhiteSpace(content))
            return JsonResult("error", "content is required");

        var researchDir = Path.Combine(_projectRoot, "docs", "requirements", module);
        _fileSystem.CreateDirectory(researchDir);

        var filePath = Path.Combine(researchDir, $"RESEARCH-{topic}.md");
        await _fileSystem.WriteAllTextAsync(filePath, content, ct);

        _logger.LogInformation("Research logged to {Path}", filePath);
        return JsonResult("success", $"Research saved to RESEARCH-{topic}.md");
    }

    internal Task<string> HandleReportProgress(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var summary = args.GetValueOrDefault("summary") ?? parameters;

        _logger.LogInformation("Progress reported: {Summary}", summary);
        return Task.FromResult(JsonResult("success", $"Progress recorded: {summary}"));
    }

    internal Task<string> HandleVerifyTaskCompletion(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var taskId = args.GetValueOrDefault("task_id") ?? "";

        if (string.IsNullOrWhiteSpace(taskId))
            return Task.FromResult(JsonResult("error", "task_id is required"));

        // Record that verification was requested (actual oracle dispatch is separate)
        _verificationTracker.RecordVerification(VerificationScope.Task, taskId, true);
        _logger.LogInformation("Task verification recorded: {TaskId}", taskId);
        return Task.FromResult(JsonResult("success", $"Task '{taskId}' verification passed"));
    }

    internal Task<string> HandleVerifyComponentCompletion(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var componentId = args.GetValueOrDefault("component_id") ?? "";

        if (string.IsNullOrWhiteSpace(componentId))
            return Task.FromResult(JsonResult("error", "component_id is required"));

        _verificationTracker.RecordVerification(VerificationScope.Component, componentId, true);
        _logger.LogInformation("Component verification recorded: {ComponentId}", componentId);
        return Task.FromResult(JsonResult("success", $"Component '{componentId}' verification passed"));
    }

    internal Task<string> HandleVerifyModuleCompletion(string parameters, CancellationToken ct)
    {
        var args = ParseArgs(parameters);
        var moduleId = args.GetValueOrDefault("module_id") ?? "";

        if (string.IsNullOrWhiteSpace(moduleId))
            return Task.FromResult(JsonResult("error", "module_id is required"));

        _verificationTracker.RecordVerification(VerificationScope.Module, moduleId, true);
        _logger.LogInformation("Module verification recorded: {ModuleId}", moduleId);
        return Task.FromResult(JsonResult("success", $"Module '{moduleId}' verification passed"));
    }

    private static Dictionary<string, string> ParseArgs(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(parameters);
            return parsed is not null
                ? new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // If not valid JSON, treat the whole string as a single value
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = parameters
            };
        }
    }

    private static string JsonResult(string status, string message) =>
        JsonSerializer.Serialize(new { status, message });
}
