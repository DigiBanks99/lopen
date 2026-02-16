using System.Diagnostics;

namespace Lopen.Otel.Tests;

/// <summary>
/// Tests for SpanFactory verifying the OTEL span hierarchy, attributes, and parent-child relationships.
/// Covers JOB-099 acceptance criteria.
/// </summary>
[Collection("ActivityListener")]
public class SpanFactoryTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];

    public SpanFactoryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => _activities.Add(a)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    // ==================== Command Span ====================

    [Fact]
    public void StartCommand_CreatesActivityWithCorrectName()
    {
        using var cmd = SpanFactory.StartCommand("spec");
        Assert.NotNull(cmd);
        Assert.Equal("lopen.command", cmd!.OperationName);
    }

    [Fact]
    public void StartCommand_SetsRequiredAttributes()
    {
        using var cmd = SpanFactory.StartCommand("build", headless: true, hasPrompt: true);
        Assert.Equal("build", cmd!.GetTagItem("lopen.command.name"));
        Assert.Equal(true, cmd.GetTagItem("lopen.command.headless"));
        Assert.Equal(true, cmd.GetTagItem("lopen.command.has_prompt"));
    }

    [Fact]
    public void SetCommandExitCode_SetsAttribute()
    {
        using var cmd = SpanFactory.StartCommand("plan");
        SpanFactory.SetCommandExitCode(cmd, 42);
        Assert.Equal(42, cmd!.GetTagItem("lopen.command.exit_code"));
    }

    // ==================== Workflow Phase Span ====================

    [Fact]
    public void StartWorkflowPhase_CreatesActivityWithAttributes()
    {
        using var cmd = SpanFactory.StartCommand("spec");
        using var phase = SpanFactory.StartWorkflowPhase("spec", "auth", iteration: 2);

        Assert.NotNull(phase);
        Assert.Equal("lopen.workflow.phase", phase!.OperationName);
        Assert.Equal("spec", phase.GetTagItem("lopen.workflow.phase"));
        Assert.Equal("auth", phase.GetTagItem("lopen.workflow.module"));
        Assert.Equal(2, phase.GetTagItem("lopen.workflow.iteration"));
    }

    [Fact]
    public void WorkflowPhase_IsChildOfCommand()
    {
        using var cmd = SpanFactory.StartCommand("spec");
        using var phase = SpanFactory.StartWorkflowPhase("spec", "auth");

        Assert.Equal(cmd!.Context.TraceId, phase!.Context.TraceId);
        Assert.Equal(cmd.Context.SpanId, phase.ParentSpanId);
    }

    // ==================== SDK Invocation Span ====================

    [Fact]
    public void StartSdkInvocation_CreatesWithModel()
    {
        using var sdk = SpanFactory.StartSdkInvocation("claude-opus-4.6");
        Assert.NotNull(sdk);
        Assert.Equal("lopen.sdk.invocation", sdk!.OperationName);
        Assert.Equal("claude-opus-4.6", sdk.GetTagItem("lopen.sdk.model"));
    }

    [Fact]
    public void SetSdkResult_SetsAllTokenAttributes()
    {
        using var sdk = SpanFactory.StartSdkInvocation("gpt-4o");
        SpanFactory.SetSdkResult(sdk, inputTokens: 500, outputTokens: 200, isPremium: true, toolCalls: 3);

        Assert.Equal(500, sdk!.GetTagItem("lopen.sdk.tokens.input"));
        Assert.Equal(200, sdk.GetTagItem("lopen.sdk.tokens.output"));
        Assert.Equal(700, sdk.GetTagItem("lopen.sdk.tokens.total"));
        Assert.Equal(true, sdk.GetTagItem("lopen.sdk.is_premium"));
        Assert.Equal(3, sdk.GetTagItem("lopen.sdk.tool_calls"));
    }

    [Fact]
    public void SdkInvocation_IsChildOfPhase()
    {
        using var cmd = SpanFactory.StartCommand("build");
        using var phase = SpanFactory.StartWorkflowPhase("build", "core");
        using var sdk = SpanFactory.StartSdkInvocation("claude-sonnet-4");

        Assert.Equal(phase!.Context.SpanId, sdk!.ParentSpanId);
    }

    // ==================== Tool Span ====================

    [Fact]
    public void StartTool_CreatesWithToolName()
    {
        using var tool = SpanFactory.StartTool("read_spec", module: "auth");
        Assert.NotNull(tool);
        Assert.Contains("lopen.tool.read_spec", tool!.OperationName);
        Assert.Equal("read_spec", tool.GetTagItem("lopen.tool.name"));
        Assert.Equal("auth", tool.GetTagItem("lopen.tool.module"));
    }

    [Fact]
    public void SetToolResult_Success()
    {
        using var tool = SpanFactory.StartTool("update_task_status");
        SpanFactory.SetToolResult(tool, success: true);

        Assert.Equal(true, tool!.GetTagItem("lopen.tool.success"));
        Assert.Null(tool.GetTagItem("lopen.tool.error"));
    }

    [Fact]
    public void SetToolResult_Failure()
    {
        using var tool = SpanFactory.StartTool("verify_task_completion");
        SpanFactory.SetToolResult(tool, success: false, error: "Test failed");

        Assert.Equal(false, tool!.GetTagItem("lopen.tool.success"));
        Assert.Equal("Test failed", tool.GetTagItem("lopen.tool.error"));
    }

    [Fact]
    public void Tool_IsChildOfSdkInvocation()
    {
        using var cmd = SpanFactory.StartCommand("build");
        using var phase = SpanFactory.StartWorkflowPhase("build", "core");
        using var sdk = SpanFactory.StartSdkInvocation("claude-opus-4.6");
        using var tool = SpanFactory.StartTool("read_spec");

        Assert.Equal(sdk!.Context.SpanId, tool!.ParentSpanId);
    }

    // ==================== Oracle Verification Span ====================

    [Fact]
    public void StartOracleVerification_CreatesWithAttributes()
    {
        using var oracle = SpanFactory.StartOracleVerification("task", "gpt-4o", attempt: 2);
        Assert.NotNull(oracle);
        Assert.Equal("lopen.oracle.verification", oracle!.OperationName);
        Assert.Equal("task", oracle.GetTagItem("lopen.oracle.scope"));
        Assert.Equal("gpt-4o", oracle.GetTagItem("lopen.oracle.model"));
        Assert.Equal(2, oracle.GetTagItem("lopen.oracle.attempt"));
    }

    [Fact]
    public void SetOracleVerdict_SetsAttribute()
    {
        using var oracle = SpanFactory.StartOracleVerification("component", "gpt-4o");
        SpanFactory.SetOracleVerdict(oracle, "pass");
        Assert.Equal("pass", oracle!.GetTagItem("lopen.oracle.verdict"));
    }

    // ==================== Task Execution Span ====================

    [Fact]
    public void StartTask_CreatesWithAttributes()
    {
        using var task = SpanFactory.StartTask("Implement login flow", "auth_handler", "auth");
        Assert.NotNull(task);
        Assert.Equal("lopen.task.execution", task!.OperationName);
        Assert.Equal("Implement login flow", task.GetTagItem("lopen.task.name"));
        Assert.Equal("auth_handler", task.GetTagItem("lopen.task.component"));
        Assert.Equal("auth", task.GetTagItem("lopen.task.module"));
    }

    [Fact]
    public void SetTaskResult_SetsOutcomeAndIterations()
    {
        using var task = SpanFactory.StartTask("Build feature", "comp", "mod");
        SpanFactory.SetTaskResult(task, "complete", iterations: 3);

        Assert.Equal("complete", task!.GetTagItem("lopen.task.outcome"));
        Assert.Equal(3, task.GetTagItem("lopen.task.iterations"));
    }

    // ==================== Session Span ====================

    [Fact]
    public void StartSession_CreatesWithAttributes()
    {
        using var session = SpanFactory.StartSession("sess-123", "save");
        Assert.NotNull(session);
        Assert.Equal("lopen.session.save", session!.OperationName);
        Assert.Equal("sess-123", session.GetTagItem("lopen.session.id"));
        Assert.Equal("save", session.GetTagItem("lopen.session.operation"));
    }

    // ==================== Git Span ====================

    [Fact]
    public void StartGit_CreatesWithAttributes()
    {
        using var git = SpanFactory.StartGit("commit", branch: "main");
        Assert.NotNull(git);
        Assert.Equal("lopen.git.commit", git!.OperationName);
        Assert.Equal("commit", git.GetTagItem("lopen.git.operation"));
        Assert.Equal("main", git.GetTagItem("lopen.git.branch"));
    }

    // ==================== Backpressure Span ====================

    [Fact]
    public void StartBackpressure_CreatesWithAttributes()
    {
        using var bp = SpanFactory.StartBackpressure("resource_limits", "budget_warning", "warn");
        Assert.NotNull(bp);
        Assert.Equal("lopen.backpressure.event", bp!.OperationName);
        Assert.Equal("resource_limits", bp.GetTagItem("lopen.backpressure.category"));
        Assert.Equal("budget_warning", bp.GetTagItem("lopen.backpressure.trigger"));
        Assert.Equal("warn", bp.GetTagItem("lopen.backpressure.action"));
    }

    // ==================== Full Hierarchy ====================

    [Fact]
    public void FullHierarchy_Command_Phase_Sdk_Tool()
    {
        using var cmd = SpanFactory.StartCommand("build");
        using var phase = SpanFactory.StartWorkflowPhase("build", "auth");
        using var sdk = SpanFactory.StartSdkInvocation("claude-opus-4.6");
        using var tool = SpanFactory.StartTool("read_spec");

        // All share the same trace
        Assert.Equal(cmd!.Context.TraceId, phase!.Context.TraceId);
        Assert.Equal(cmd.Context.TraceId, sdk!.Context.TraceId);
        Assert.Equal(cmd.Context.TraceId, tool!.Context.TraceId);

        // Parent-child: cmd → phase → sdk → tool
        Assert.Equal(cmd.Context.SpanId, phase.ParentSpanId);
        Assert.Equal(phase.Context.SpanId, sdk.ParentSpanId);
        Assert.Equal(sdk.Context.SpanId, tool.ParentSpanId);
    }

    [Fact]
    public void FullHierarchy_Command_Phase_Task_Sdk_Git()
    {
        using var cmd = SpanFactory.StartCommand("build");
        using var phase = SpanFactory.StartWorkflowPhase("build", "core");
        using var task = SpanFactory.StartTask("Implement feature", "comp", "core");
        using var sdk = SpanFactory.StartSdkInvocation("gpt-4o");

        Assert.Equal(phase!.Context.SpanId, task!.ParentSpanId);
        Assert.Equal(task.Context.SpanId, sdk!.ParentSpanId);

        sdk.Dispose();

        using var git = SpanFactory.StartGit("commit", "feat/x");
        Assert.Equal(task.Context.SpanId, git!.ParentSpanId);
    }

    // ==================== Null Safety ====================

    [Fact]
    public void SetCommandExitCode_NullActivity_DoesNotThrow()
    {
        SpanFactory.SetCommandExitCode(null, 0);
    }

    [Fact]
    public void SetSdkResult_NullActivity_DoesNotThrow()
    {
        SpanFactory.SetSdkResult(null, 0, 0, false, 0);
    }

    [Fact]
    public void SetToolResult_NullActivity_DoesNotThrow()
    {
        SpanFactory.SetToolResult(null, true);
    }

    [Fact]
    public void SetOracleVerdict_NullActivity_DoesNotThrow()
    {
        SpanFactory.SetOracleVerdict(null, "pass");
    }

    [Fact]
    public void SetTaskResult_NullActivity_DoesNotThrow()
    {
        SpanFactory.SetTaskResult(null, "complete", 1);
    }
}
