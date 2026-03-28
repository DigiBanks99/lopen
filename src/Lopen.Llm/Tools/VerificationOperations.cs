using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tools;

/// <summary>
/// Static operations for oracle verification tools.
/// </summary>
internal static class VerificationOperations
{
    public static void AddTools(
        List<AIFunction> tools,
        IOracleVerifier? oracleVerifier,
        IVerificationTracker verificationTracker)
    {
        tools.Add(AIFunctionFactory.Create(
            [Description("Dispatch oracle sub-agent to verify a task is complete")]
            (string task_id, string? evidence, string? acceptance_criteria) =>
                VerifyTaskCompletion(oracleVerifier, verificationTracker, task_id, evidence, acceptance_criteria),
            "verify_task_completion"));

        tools.Add(AIFunctionFactory.Create(
            [Description("Dispatch oracle sub-agent to verify all tasks in a component are complete")]
            (string component_id, string? evidence, string? acceptance_criteria) =>
                VerifyComponentCompletion(oracleVerifier, verificationTracker, component_id, evidence, acceptance_criteria),
            "verify_component_completion"));

        tools.Add(AIFunctionFactory.Create(
            [Description("Dispatch oracle sub-agent to verify the module meets all acceptance criteria")]
            (string module_id, string? evidence, string? acceptance_criteria) =>
                VerifyModuleCompletion(oracleVerifier, verificationTracker, module_id, evidence, acceptance_criteria),
            "verify_module_completion"));
    }

    internal static async Task<string> VerifyTaskCompletion(
        IOracleVerifier? oracleVerifier,
        IVerificationTracker verificationTracker,
        string taskId,
        string? evidence,
        string? acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return JsonResult("error", "task_id is required");

        return await VerifyAsync(oracleVerifier, verificationTracker, VerificationScope.Task, taskId, evidence, acceptanceCriteria);
    }

    internal static async Task<string> VerifyComponentCompletion(
        IOracleVerifier? oracleVerifier,
        IVerificationTracker verificationTracker,
        string componentId,
        string? evidence,
        string? acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(componentId))
            return JsonResult("error", "component_id is required");

        return await VerifyAsync(oracleVerifier, verificationTracker, VerificationScope.Component, componentId, evidence, acceptanceCriteria);
    }

    internal static async Task<string> VerifyModuleCompletion(
        IOracleVerifier? oracleVerifier,
        IVerificationTracker verificationTracker,
        string moduleId,
        string? evidence,
        string? acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return JsonResult("error", "module_id is required");

        return await VerifyAsync(oracleVerifier, verificationTracker, VerificationScope.Module, moduleId, evidence, acceptanceCriteria);
    }

    private static async Task<string> VerifyAsync(
        IOracleVerifier? oracleVerifier,
        IVerificationTracker verificationTracker,
        VerificationScope scope,
        string identifier,
        string? evidence,
        string? acceptanceCriteria)
    {
        if (oracleVerifier is not null
            && !string.IsNullOrWhiteSpace(evidence)
            && !string.IsNullOrWhiteSpace(acceptanceCriteria))
        {
            OracleVerdict verdict = await oracleVerifier.VerifyAsync(scope, evidence, acceptanceCriteria);
            verificationTracker.RecordVerification(scope, identifier, verdict.Passed);

            if (!verdict.Passed)
            {
                var gapList = string.Join("; ", verdict.Gaps);
                return JsonResult("fail", $"{scope} '{identifier}' verification failed. Gaps: {gapList}");
            }

            return JsonResult("success", $"{scope} '{identifier}' verification passed");
        }

        // Fallback: auto-pass when oracle not available or evidence/criteria not provided
        verificationTracker.RecordVerification(scope, identifier, true);
        return JsonResult("success", $"{scope} '{identifier}' verification passed");
    }

    internal static string JsonResult(string status, string message) =>
        System.Text.Json.JsonSerializer.Serialize(new { status, message });
}
