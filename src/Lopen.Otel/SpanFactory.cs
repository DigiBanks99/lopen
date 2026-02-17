using System.Diagnostics;

namespace Lopen.Otel;

/// <summary>
/// Factory methods for creating properly attributed OTEL spans.
/// Each method creates an Activity with the correct parent-child hierarchy and required attributes.
/// </summary>
public static class SpanFactory
{
    /// <summary>
    /// Creates a root command span. All other spans are children of this.
    /// </summary>
    public static Activity? StartCommand(string commandName, bool headless = false, bool hasPrompt = false)
    {
        var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.command", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.command.name", commandName);
            activity.SetTag("lopen.command.headless", headless);
            activity.SetTag("lopen.command.has_prompt", hasPrompt);
        }
        return activity;
    }

    /// <summary>
    /// Sets the exit code on a command span before it ends.
    /// </summary>
    public static void SetCommandExitCode(Activity? activity, int exitCode)
    {
        activity?.SetTag("lopen.command.exit_code", exitCode);
    }

    /// <summary>
    /// Creates a workflow phase span (child of command span).
    /// </summary>
    public static Activity? StartWorkflowPhase(string phase, string module, int iteration = 1)
    {
        var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.workflow.phase", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.workflow.phase", phase);
            activity.SetTag("lopen.workflow.module", module);
            activity.SetTag("lopen.workflow.iteration", iteration);
        }
        return activity;
    }

    /// <summary>
    /// Creates an SDK invocation span (child of workflow phase or task span).
    /// </summary>
    public static Activity? StartSdkInvocation(string model)
    {
        var activity = LopenTelemetryDiagnostics.Sdk.StartActivity("lopen.sdk.invocation", ActivityKind.Client);
        if (activity is not null)
        {
            activity.SetTag("lopen.sdk.model", model);
        }
        return activity;
    }

    /// <summary>
    /// Sets token usage and tool call count on an SDK invocation span.
    /// </summary>
    public static void SetSdkResult(Activity? activity, int inputTokens, int outputTokens, bool isPremium, int toolCalls)
    {
        if (activity is null)
            return;
        activity.SetTag("lopen.sdk.tokens.input", inputTokens);
        activity.SetTag("lopen.sdk.tokens.output", outputTokens);
        activity.SetTag("lopen.sdk.tokens.total", inputTokens + outputTokens);
        activity.SetTag("lopen.sdk.is_premium", isPremium);
        activity.SetTag("lopen.sdk.tool_calls", toolCalls);
    }

    /// <summary>
    /// Creates a tool execution span (child of SDK invocation).
    /// </summary>
    public static Activity? StartTool(string toolName, string? module = null)
    {
        var activity = LopenTelemetryDiagnostics.Tools.StartActivity($"lopen.tool.{toolName}", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.tool.name", toolName);
            if (module is not null)
                activity.SetTag("lopen.tool.module", module);
        }
        return activity;
    }

    /// <summary>
    /// Sets the outcome of a tool execution.
    /// </summary>
    public static void SetToolResult(Activity? activity, bool success, string? error = null)
    {
        if (activity is null)
            return;
        activity.SetTag("lopen.tool.success", success);
        if (error is not null)
            activity.SetTag("lopen.tool.error", error);
    }

    /// <summary>
    /// Creates an oracle verification span (child of SDK invocation).
    /// </summary>
    public static Activity? StartOracleVerification(string scope, string model, int attempt = 1)
    {
        var activity = LopenTelemetryDiagnostics.Oracle.StartActivity("lopen.oracle.verification", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.oracle.scope", scope);
            activity.SetTag("lopen.oracle.model", model);
            activity.SetTag("lopen.oracle.attempt", attempt);
        }
        return activity;
    }

    /// <summary>
    /// Sets the verdict on an oracle verification span.
    /// </summary>
    public static void SetOracleVerdict(Activity? activity, string verdict)
    {
        activity?.SetTag("lopen.oracle.verdict", verdict);
    }

    /// <summary>
    /// Creates a task execution span (child of workflow phase).
    /// </summary>
    public static Activity? StartTask(string taskName, string component, string module)
    {
        var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.task.execution", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.task.name", taskName);
            activity.SetTag("lopen.task.component", component);
            activity.SetTag("lopen.task.module", module);
        }
        return activity;
    }

    /// <summary>
    /// Sets the outcome of a task execution.
    /// </summary>
    public static void SetTaskResult(Activity? activity, string outcome, int iterations)
    {
        if (activity is null)
            return;
        activity.SetTag("lopen.task.outcome", outcome);
        activity.SetTag("lopen.task.iterations", iterations);
    }

    /// <summary>
    /// Creates a session save/load span.
    /// </summary>
    public static Activity? StartSession(string sessionId, string operation)
    {
        var activity = LopenTelemetryDiagnostics.Session.StartActivity("lopen.session.save", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.session.id", sessionId);
            activity.SetTag("lopen.session.operation", operation);
        }
        return activity;
    }

    /// <summary>
    /// Creates a git operation span.
    /// </summary>
    public static Activity? StartGit(string operation, string? branch = null)
    {
        var activity = LopenTelemetryDiagnostics.Git.StartActivity("lopen.git.commit", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.git.operation", operation);
            if (branch is not null)
                activity.SetTag("lopen.git.branch", branch);
        }
        return activity;
    }

    /// <summary>
    /// Creates a backpressure event span.
    /// </summary>
    public static Activity? StartBackpressure(string category, string trigger, string action)
    {
        var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.backpressure.event", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("lopen.backpressure.category", category);
            activity.SetTag("lopen.backpressure.trigger", trigger);
            activity.SetTag("lopen.backpressure.action", action);
        }
        return activity;
    }
}
