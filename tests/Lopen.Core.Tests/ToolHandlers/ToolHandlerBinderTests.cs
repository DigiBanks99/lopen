using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Lopen.Core.Documents;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.ToolHandlers;

public class ToolHandlerBinderTests
{
    private readonly StubFileSystem _fileSystem = new();
    private readonly StubSectionExtractor _sectionExtractor = new();
    private readonly StubWorkflowEngine _engine = new();
    private readonly StubVerificationTracker _verificationTracker = new();
    private const string ProjectRoot = "/test/project";

    private ToolHandlerBinder CreateBinder(
        StubGitWorkflowService? git = null,
        StubTaskStatusGate? gate = null,
        StubPlanManager? planManager = null,
        StubOracleVerifier? oracleVerifier = null) => new(
        _fileSystem, _sectionExtractor, _engine, _verificationTracker,
        NullLogger<ToolHandlerBinder>.Instance, ProjectRoot,
        git, gate, planManager, oracleVerifier);

    [Fact]
    public void BindAll_BindsAllTenTools()
    {
        var registry = new TrackingToolRegistry();
        var binder = CreateBinder();

        binder.BindAll(registry);

        Assert.Equal(10, registry.BoundHandlers.Count);
        Assert.Contains("read_spec", registry.BoundHandlers.Keys);
        Assert.Contains("read_research", registry.BoundHandlers.Keys);
        Assert.Contains("read_plan", registry.BoundHandlers.Keys);
        Assert.Contains("update_task_status", registry.BoundHandlers.Keys);
        Assert.Contains("get_current_context", registry.BoundHandlers.Keys);
        Assert.Contains("log_research", registry.BoundHandlers.Keys);
        Assert.Contains("report_progress", registry.BoundHandlers.Keys);
        Assert.Contains("verify_task_completion", registry.BoundHandlers.Keys);
        Assert.Contains("verify_component_completion", registry.BoundHandlers.Keys);
        Assert.Contains("verify_module_completion", registry.BoundHandlers.Keys);
    }

    [Fact]
    public void BindAll_ThrowsOnNullRegistry()
    {
        var binder = CreateBinder();
        Assert.Throws<ArgumentNullException>(() => binder.BindAll(null!));
    }

    [Fact]
    public async Task HandleReadSpec_ReturnsSpecContent()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/SPECIFICATION.md"] = "# Core Spec\nContent here";
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("""{"module":"core"}""", CancellationToken.None);
        Assert.Contains("Core Spec", result);
    }

    [Fact]
    public async Task HandleReadSpec_ReturnsErrorWhenNotFound()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("""{"module":"missing"}""", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleReadSpec_ExtractsSectionWhenSpecified()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/SPECIFICATION.md"] = "# Spec\n## Overview\nHello";
        _sectionExtractor.Sections = [new ExtractedSection("Overview", "Hello", 1)];
        var binder = CreateBinder();

        var result = await binder.HandleReadSpec("""{"module":"core","section":"Overview"}""", CancellationToken.None);
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task HandleReadResearch_ReturnsMainResearch()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/RESEARCH.md"] = "# Research";
        _fileSystem.Directories.Add("/test/project/docs/requirements/core");
        var binder = CreateBinder();

        var result = await binder.HandleReadResearch("""{"module":"core"}""", CancellationToken.None);
        Assert.Contains("Research", result);
    }

    [Fact]
    public async Task HandleReadResearch_ReturnsTopicResearch()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/RESEARCH-api.md"] = "# API Research";
        _fileSystem.Directories.Add("/test/project/docs/requirements/core");
        var binder = CreateBinder();

        var result = await binder.HandleReadResearch("""{"module":"core","topic":"api"}""", CancellationToken.None);
        Assert.Contains("API Research", result);
    }

    [Fact]
    public async Task HandleReadPlan_ReturnsPlanContent()
    {
        _fileSystem.Files["/test/project/docs/requirements/IMPLEMENTATION_PLAN.md"] = "# Plan";
        var binder = CreateBinder();

        var result = await binder.HandleReadPlan("", CancellationToken.None);
        Assert.Contains("Plan", result);
    }

    [Fact]
    public async Task HandleReadPlan_ReturnsErrorWhenMissing()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReadPlan("", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_RejectsCompleteWithoutVerification()
    {
        _verificationTracker.VerifiedItems.Clear();
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete"}""", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("verify_task_completion", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_AcceptsCompleteWithVerification()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-1"));
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete"}""", CancellationToken.None);
        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_AcceptsNonCompleteStatus()
    {
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"in-progress"}""", CancellationToken.None);
        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_ReturnsErrorOnMissingParams()
    {
        var binder = CreateBinder();
        var result = await binder.HandleUpdateTaskStatus("", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_CommitsOnCompletionWhenGitServiceProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-1"));
        var gitService = new StubGitWorkflowService();
        var binder = CreateBinder(git: gitService);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete","module":"core","component":"workflow"}""",
            CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Single(gitService.Commits);
        Assert.Equal("core", gitService.Commits[0].Module);
        Assert.Equal("workflow", gitService.Commits[0].Component);
        Assert.Equal("task-1", gitService.Commits[0].Task);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_SkipsCommitWhenModuleNotProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-2"));
        var gitService = new StubGitWorkflowService();
        var binder = CreateBinder(git: gitService);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-2","status":"complete"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Empty(gitService.Commits);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_SkipsCommitWhenGitServiceNotProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-3"));
        var binder = CreateBinder(); // No git service

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-3","status":"complete","module":"core"}""", CancellationToken.None);

        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_DoesNotCommitForNonCompleteStatus()
    {
        var gitService = new StubGitWorkflowService();
        var binder = CreateBinder(git: gitService);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"in-progress","module":"core"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Empty(gitService.Commits);
    }

    [Fact]
    public async Task HandleGetCurrentContext_ReturnsWorkflowState()
    {
        _engine.CurrentStep = WorkflowStep.IdentifyComponents;
        var binder = CreateBinder();

        var result = await binder.HandleGetCurrentContext("", CancellationToken.None);
        Assert.Contains("IdentifyComponents", result);
        Assert.Contains("Planning", result);
    }

    [Fact]
    public async Task HandleLogResearch_WritesFile()
    {
        var binder = CreateBinder();

        var result = await binder.HandleLogResearch(
            """{"module":"core","topic":"api","content":"# API findings"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.True(_fileSystem.Files.ContainsKey("/test/project/docs/requirements/core/RESEARCH-api.md"));
    }

    [Fact]
    public async Task HandleLogResearch_ReturnsErrorOnEmptyContent()
    {
        var binder = CreateBinder();
        var result = await binder.HandleLogResearch("""{"module":"core","topic":"api","content":""}""", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleReportProgress_ReturnsSuccess()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReportProgress("""{"summary":"Made progress"}""", CancellationToken.None);
        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_RecordsVerification()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyTaskCompletion("""{"task_id":"task-1"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains(("Task", "task-1"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_ReturnsErrorOnMissingId()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyTaskCompletion("", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleVerifyComponentCompletion_RecordsVerification()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyComponentCompletion("""{"component_id":"comp-1"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains(("Component", "comp-1"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyModuleCompletion_RecordsVerification()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyModuleCompletion("""{"module_id":"core"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains(("Module", "core"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleReadSpec_HandlesInvalidJson()
    {
        _fileSystem.Files["/test/project/docs/requirements/DraftSpecification/SPECIFICATION.md"] = "# Spec";
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("not json", CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task BindAll_TracedHandlers_CreateToolSpans()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        _fileSystem.Files["/test/project/docs/requirements/IMPLEMENTATION_PLAN.md"] = "# Plan";
        var binder = CreateBinder();
        var registry = new TrackingToolRegistry();
        binder.BindAll(registry);

        // Invoke a traced handler through the registry
        var handler = registry.BoundHandlers["read_plan"];
        await handler("{}", CancellationToken.None);

        Assert.Contains(activities, a => a.OperationName == "lopen.tool.read_plan");
    }

    [Fact]
    public async Task BindAll_VerifyHandlers_CreateOracleSpans()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var binder = CreateBinder();
        var registry = new TrackingToolRegistry();
        binder.BindAll(registry);

        // Invoke verify handler
        var handler = registry.BoundHandlers["verify_task_completion"];
        await handler("""{"task_id":"t1"}""", CancellationToken.None);

        Assert.Contains(activities, a => a.OperationName == "lopen.oracle.verification");
    }

    [Fact]
    public async Task BindAll_TracedToolHandler_RecordsToolCounter()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.tools.count")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        var binder = CreateBinder();
        var registry = new TrackingToolRegistry();
        binder.BindAll(registry);

        var handler = registry.BoundHandlers["read_spec"];
        await handler("""{"module":"test"}""", CancellationToken.None);
        listener.RecordObservableInstruments();

        Assert.True(count >= 1, "Tool counter should be incremented");
    }

    [Fact]
    public async Task BindAll_TracedToolHandler_RecordsToolDuration()
    {
        double duration = -1;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.tool.duration")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => duration = measurement);
        listener.Start();

        var binder = CreateBinder();
        var registry = new TrackingToolRegistry();
        binder.BindAll(registry);

        var handler = registry.BoundHandlers["read_spec"];
        await handler("""{"module":"test"}""", CancellationToken.None);
        listener.RecordObservableInstruments();

        Assert.True(duration >= 0, "Tool duration should be recorded");
    }

    [Fact]
    public async Task BindAll_TracedVerifyHandler_RecordsOracleCounter()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.oracle.verdicts.count")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        var binder = CreateBinder();
        var registry = new TrackingToolRegistry();
        binder.BindAll(registry);

        var handler = registry.BoundHandlers["verify_task_completion"];
        await handler("""{"task_id":"t1"}""", CancellationToken.None);
        listener.RecordObservableInstruments();

        Assert.True(count >= 1, "Oracle verdict counter should be incremented");
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_UsesTaskStatusGateWhenProvided()
    {
        var gate = new StubTaskStatusGate { Result = TaskStatusGateResult.Rejected("Gate says no") };
        var binder = CreateBinder(gate: gate);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-gate","status":"complete"}""", CancellationToken.None);

        Assert.Contains("Gate says no", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_TaskStatusGateAllowed_Succeeds()
    {
        var gate = new StubTaskStatusGate { Result = TaskStatusGateResult.Allowed() };
        var binder = CreateBinder(gate: gate);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-gate","status":"complete"}""", CancellationToken.None);

        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_CallsPlanManagerOnCompletion()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-plan"));
        var planManager = new StubPlanManager();
        var binder = CreateBinder(planManager: planManager);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-plan","status":"complete","module":"core"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Single(planManager.Updates);
        Assert.Equal("core", planManager.Updates[0].Module);
        Assert.Equal("task-plan", planManager.Updates[0].TaskText);
        Assert.True(planManager.Updates[0].Completed);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_SkipsPlanManagerWhenNotProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-no-plan"));
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-no-plan","status":"complete","module":"core"}""", CancellationToken.None);

        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_DispatchesOracleWhenAvailable()
    {
        var oracle = new StubOracleVerifier { Verdict = new Lopen.Llm.OracleVerdict(true, [], Lopen.Llm.VerificationScope.Task) };
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyTaskCompletion(
            """{"task_id":"task-1","evidence":"diff here","acceptance_criteria":"must pass"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Single(oracle.Invocations);
        Assert.Equal(Lopen.Llm.VerificationScope.Task, oracle.Invocations[0].Scope);
        Assert.Equal("diff here", oracle.Invocations[0].Evidence);
        Assert.Equal("must pass", oracle.Invocations[0].AcceptanceCriteria);
        Assert.Contains(("Task", "task-1"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_OracleFails_RecordsFailure()
    {
        var oracle = new StubOracleVerifier
        {
            Verdict = new Lopen.Llm.OracleVerdict(false, ["Missing tests"], Lopen.Llm.VerificationScope.Task)
        };
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyTaskCompletion(
            """{"task_id":"task-1","evidence":"diff","acceptance_criteria":"criteria"}""", CancellationToken.None);

        Assert.Contains("fail", result);
        Assert.Contains("Missing tests", result);
        // Verification should be recorded as failed
        Assert.False(_verificationTracker.IsVerified(Lopen.Llm.VerificationScope.Task, "task-1"));
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_NoOracle_AutoPasses()
    {
        var binder = CreateBinder(); // No oracle
        var result = await binder.HandleVerifyTaskCompletion(
            """{"task_id":"task-1","evidence":"diff","acceptance_criteria":"criteria"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.True(_verificationTracker.IsVerified(Lopen.Llm.VerificationScope.Task, "task-1"));
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_OraclePresent_NoEvidence_AutoPasses()
    {
        var oracle = new StubOracleVerifier();
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyTaskCompletion(
            """{"task_id":"task-1"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Empty(oracle.Invocations); // Oracle should NOT be called
        Assert.True(_verificationTracker.IsVerified(Lopen.Llm.VerificationScope.Task, "task-1"));
    }

    [Fact]
    public async Task HandleVerifyComponentCompletion_DispatchesOracleWhenAvailable()
    {
        var oracle = new StubOracleVerifier { Verdict = new Lopen.Llm.OracleVerdict(true, [], Lopen.Llm.VerificationScope.Component) };
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyComponentCompletion(
            """{"component_id":"comp-1","evidence":"component diff","acceptance_criteria":"component criteria"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Single(oracle.Invocations);
        Assert.Equal(Lopen.Llm.VerificationScope.Component, oracle.Invocations[0].Scope);
        Assert.Contains(("Component", "comp-1"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyComponentCompletion_OracleFails_RecordsFailure()
    {
        var oracle = new StubOracleVerifier
        {
            Verdict = new Lopen.Llm.OracleVerdict(false, ["Regression found"], Lopen.Llm.VerificationScope.Component)
        };
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyComponentCompletion(
            """{"component_id":"comp-1","evidence":"diff","acceptance_criteria":"criteria"}""", CancellationToken.None);

        Assert.Contains("fail", result);
        Assert.Contains("Regression found", result);
    }

    [Fact]
    public async Task HandleVerifyModuleCompletion_DispatchesOracleWhenAvailable()
    {
        var oracle = new StubOracleVerifier { Verdict = new Lopen.Llm.OracleVerdict(true, [], Lopen.Llm.VerificationScope.Module) };
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyModuleCompletion(
            """{"module_id":"core","evidence":"module diff","acceptance_criteria":"module criteria"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Single(oracle.Invocations);
        Assert.Equal(Lopen.Llm.VerificationScope.Module, oracle.Invocations[0].Scope);
        Assert.Contains(("Module", "core"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyModuleCompletion_OracleFails_RecordsFailure()
    {
        var oracle = new StubOracleVerifier
        {
            Verdict = new Lopen.Llm.OracleVerdict(false, ["Spec not met"], Lopen.Llm.VerificationScope.Module)
        };
        var binder = CreateBinder(oracleVerifier: oracle);

        var result = await binder.HandleVerifyModuleCompletion(
            """{"module_id":"core","evidence":"diff","acceptance_criteria":"criteria"}""", CancellationToken.None);

        Assert.Contains("fail", result);
        Assert.Contains("Spec not met", result);
        Assert.False(_verificationTracker.IsVerified(Lopen.Llm.VerificationScope.Module, "core"));
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_OraclePass_ThenUpdateTaskStatus_Succeeds()
    {
        var oracle = new StubOracleVerifier { Verdict = new Lopen.Llm.OracleVerdict(true, [], Lopen.Llm.VerificationScope.Task) };
        var binder = CreateBinder(oracleVerifier: oracle);

        // First, verify the task
        await binder.HandleVerifyTaskCompletion(
            """{"task_id":"task-1","evidence":"diff","acceptance_criteria":"criteria"}""", CancellationToken.None);

        // Then, try to complete it
        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete"}""", CancellationToken.None);

        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_OracleFail_ThenUpdateTaskStatus_Rejected()
    {
        var oracle = new StubOracleVerifier
        {
            Verdict = new Lopen.Llm.OracleVerdict(false, ["Gaps exist"], Lopen.Llm.VerificationScope.Task)
        };
        var binder = CreateBinder(oracleVerifier: oracle);

        // Verify the task (will fail)
        await binder.HandleVerifyTaskCompletion(
            """{"task_id":"task-1","evidence":"diff","acceptance_criteria":"criteria"}""", CancellationToken.None);

        // Try to complete - should be rejected
        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete"}""", CancellationToken.None);

        Assert.Contains("error", result);
        Assert.Contains("verify_task_completion", result);
    }

    [Fact]
    public async Task HandleReadResearch_TopicFileNotFound_ReturnsError()
    {
        _fileSystem.Directories.Add("/test/project/docs/requirements/core");
        var binder = CreateBinder();

        var result = await binder.HandleReadResearch("""{"module":"core","topic":"missing"}""", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("RESEARCH-missing.md", result);
    }

    [Fact]
    public async Task HandleReadResearch_DirectoryNotFound_ReturnsError()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReadResearch("""{"module":"nonexistent"}""", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("Research directory not found", result);
    }

    [Fact]
    public async Task HandleReadSpec_SectionNoMatch_ReturnsError()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/SPECIFICATION.md"] = "# Spec\n## Other\nContent";
        _sectionExtractor.Sections = [];
        var binder = CreateBinder();

        var result = await binder.HandleReadSpec("""{"module":"core","section":"NonExistent"}""", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("NonExistent", result);
        Assert.Contains("not found in specification", result);
    }

    [Fact]
    public async Task HandleReadSpec_MissingModuleParam_ReturnsError()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("{}", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("Specification not found", result);
    }

    [Fact]
    public async Task HandleVerifyComponentCompletion_MissingComponentId_ReturnsError()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyComponentCompletion("{}", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("component_id is required", result);
    }

    [Fact]
    public async Task HandleVerifyModuleCompletion_MissingModuleId_ReturnsError()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyModuleCompletion("{}", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("module_id is required", result);
    }

    [Fact]
    public async Task HandleLogResearch_DefaultTopicWhenNotSpecified()
    {
        var binder = CreateBinder();

        var result = await binder.HandleLogResearch("""{"module":"core","content":"# Findings"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains("RESEARCH-general.md", result);
        Assert.True(_fileSystem.Files.ContainsKey("/test/project/docs/requirements/core/RESEARCH-general.md"));
    }

    [Fact]
    public async Task HandleLogResearch_MissingParams_ReturnsError()
    {
        var binder = CreateBinder();
        var result = await binder.HandleLogResearch("{}", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("content is required", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_MissingTaskId_ReturnsError()
    {
        var binder = CreateBinder();
        var result = await binder.HandleUpdateTaskStatus("""{"status":"in-progress"}""", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("task_id and status are required", result);
    }

    [Fact]
    public async Task HandleReportProgress_MissingSummary_FallsBackToRawParameters()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReportProgress("raw progress text", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains("raw progress text", result);
    }

    [Fact]
    public async Task HandleGetCurrentContext_ReturnsJsonWithAllFields()
    {
        _engine.CurrentStep = WorkflowStep.DraftSpecification;
        _engine.IsComplete = false;
        var binder = CreateBinder();

        var result = await binder.HandleGetCurrentContext("{}", CancellationToken.None);
        var context = JsonSerializer.Deserialize<Dictionary<string, string>>(result);

        Assert.NotNull(context);
        Assert.Equal("DraftSpecification", context["step"]);
        Assert.Equal("RequirementGathering", context["phase"]);
        Assert.Equal("False", context["is_complete"]);
        Assert.Contains("permitted_triggers", context.Keys);
    }

    [Fact]
    public void BindHandler_UnknownToolName_ReturnsFalse()
    {
        var registry = new RegisteredOnlyToolRegistry();
        var result = registry.BindHandler("nonexistent_tool", (_, _) => Task.FromResult(""));
        Assert.False(result);
    }

    // --- SanitizeTopicSlug tests ---

    [Fact]
    public void SanitizeTopicSlug_RemovesSpecialChars()
    {
        Assert.Equal("hello-world", ToolHandlerBinder.SanitizeTopicSlug("hello world!"));
    }

    [Fact]
    public void SanitizeTopicSlug_CollapsesMultipleHyphens()
    {
        Assert.Equal("foo-bar", ToolHandlerBinder.SanitizeTopicSlug("foo--bar"));
    }

    [Fact]
    public void SanitizeTopicSlug_TrimsHyphens()
    {
        Assert.Equal("test", ToolHandlerBinder.SanitizeTopicSlug("-test-"));
    }

    [Fact]
    public void SanitizeTopicSlug_PreservesValidChars()
    {
        Assert.Equal("auth-jwt_v2", ToolHandlerBinder.SanitizeTopicSlug("auth-jwt_v2"));
    }

    // --- ResearchIndex tests ---

    [Fact]
    public async Task HandleLogResearch_SanitizesTopic_InFilename()
    {
        var binder = CreateBinder();

        var result = await binder.HandleLogResearch(
            """{"module":"core","topic":"my research topic","content":"# Content"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.True(_fileSystem.Files.ContainsKey("/test/project/docs/requirements/core/RESEARCH-my-research-topic.md"));
    }

    [Fact]
    public async Task HandleLogResearch_CreatesResearchIndex()
    {
        var binder = CreateBinder();

        await binder.HandleLogResearch(
            """{"module":"core","topic":"api","content":"# API findings"}""", CancellationToken.None);

        var indexPath = "/test/project/docs/requirements/core/RESEARCH.md";
        Assert.True(_fileSystem.Files.ContainsKey(indexPath));
        var index = _fileSystem.Files[indexPath];
        Assert.Contains("Research Index", index);
        Assert.Contains("[api](RESEARCH-api.md)", index);
    }

    [Fact]
    public async Task HandleLogResearch_IndexContainsAllFiles()
    {
        var binder = CreateBinder();

        await binder.HandleLogResearch(
            """{"module":"core","topic":"api","content":"# API"}""", CancellationToken.None);
        await binder.HandleLogResearch(
            """{"module":"core","topic":"auth","content":"# Auth"}""", CancellationToken.None);

        var indexPath = "/test/project/docs/requirements/core/RESEARCH.md";
        var index = _fileSystem.Files[indexPath];
        Assert.Contains("[api](RESEARCH-api.md)", index);
        Assert.Contains("[auth](RESEARCH-auth.md)", index);
    }

    [Fact]
    public async Task UpdateResearchIndex_EmptyDirectory_NoIndexCreated()
    {
        var binder = CreateBinder();
        var emptyDir = "/test/project/docs/requirements/empty";

        await binder.UpdateResearchIndexAsync(emptyDir, CancellationToken.None);

        Assert.False(_fileSystem.Files.ContainsKey(Path.Combine(emptyDir, "RESEARCH.md")));
    }

    // --- Stubs ---

    private sealed class RegisteredOnlyToolRegistry : IToolRegistry
    {
        private readonly HashSet<string> _registered = new(StringComparer.OrdinalIgnoreCase);
        public void RegisterTool(LopenToolDefinition tool) => _registered.Add(tool.Name);
        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase) => [];
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => [];
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler) =>
            _registered.Contains(toolName);
    }

    private sealed class StubOracleVerifier : Lopen.Llm.IOracleVerifier
    {
        public Lopen.Llm.OracleVerdict Verdict { get; set; } =
            new(true, [], Lopen.Llm.VerificationScope.Task);

        public List<(Lopen.Llm.VerificationScope Scope, string Evidence, string AcceptanceCriteria)> Invocations { get; } = [];

        public Task<Lopen.Llm.OracleVerdict> VerifyAsync(
            Lopen.Llm.VerificationScope scope, string evidence, string acceptanceCriteria,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add((scope, evidence, acceptanceCriteria));
            return Task.FromResult(new Lopen.Llm.OracleVerdict(Verdict.Passed, Verdict.Gaps, scope));
        }
    }

    private sealed class StubGitWorkflowService : Lopen.Core.Git.IGitWorkflowService
    {
        public List<(string Module, string Component, string Task)> Commits { get; } = [];
        public Lopen.Core.Git.GitResult? CommitResult { get; set; } = new(0, "committed", "");

        public Task<Lopen.Core.Git.GitResult?> EnsureModuleBranchAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult<Lopen.Core.Git.GitResult?>(new(0, "branch created", ""));

        public Task<Lopen.Core.Git.GitResult?> CommitTaskCompletionAsync(string moduleName, string componentName, string taskName, CancellationToken ct = default)
        {
            Commits.Add((moduleName, componentName, taskName));
            return Task.FromResult(CommitResult);
        }

        public string FormatCommitMessage(string moduleName, string componentName, string taskName) =>
            $"feat({moduleName}): complete {taskName}";
    }

    private sealed class StubFileSystem : Lopen.Storage.IFileSystem
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CreatedDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public bool FileExists(string path) => Files.ContainsKey(path);
        public bool DirectoryExists(string path) => Directories.Contains(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
            Files.TryGetValue(path, out var content)
                ? Task.FromResult(content)
                : Task.FromException<string>(new FileNotFoundException(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
        {
            Files[path] = content;
            return Task.CompletedTask;
        }
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*")
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern).Replace("\\*", ".*") + "$";
            return Files.Keys
                .Where(f => f.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                .Where(f => System.Text.RegularExpressions.Regex.IsMatch(
                    System.IO.Path.GetFileName(f), regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        }
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string source, string dest) { }
        public void DeleteFile(string path) => Files.Remove(path);
        public void DeleteDirectory(string path, bool recursive = true) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }

    private sealed class StubSectionExtractor : ISectionExtractor
    {
        public IReadOnlyList<ExtractedSection> Sections { get; set; } = [];

        public IReadOnlyList<ExtractedSection> ExtractRelevantSections(string specContent, IReadOnlyList<string> relevantHeaders) =>
            Sections;

        public IReadOnlyList<ExtractedSection> ExtractAllSections(string specContent) => Sections;
    }

    private sealed class StubWorkflowEngine : IWorkflowEngine
    {
        public WorkflowStep CurrentStep { get; set; } = WorkflowStep.DraftSpecification;
        public WorkflowPhase CurrentPhase => CurrentStep switch
        {
            WorkflowStep.DraftSpecification => WorkflowPhase.RequirementGathering,
            WorkflowStep.IterateThroughTasks or WorkflowStep.Repeat => WorkflowPhase.Building,
            _ => WorkflowPhase.Planning
        };
        public bool IsComplete { get; set; }

        public Task InitializeAsync(string moduleName, CancellationToken ct = default) => Task.CompletedTask;
        public bool Fire(WorkflowTrigger trigger) => true;
        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() => [WorkflowTrigger.Assess];
    }

    private sealed class StubVerificationTracker : IVerificationTracker
    {
        public HashSet<(string Scope, string Id)> VerifiedItems { get; } = [];
        public List<(string Scope, string Id)> RecordedVerifications { get; } = [];

        public void RecordVerification(VerificationScope scope, string identifier, bool passed)
        {
            RecordedVerifications.Add((scope.ToString(), identifier));
            if (passed) VerifiedItems.Add((scope.ToString(), identifier));
        }

        public bool IsVerified(VerificationScope scope, string identifier) =>
            VerifiedItems.Contains((scope.ToString(), identifier));

        public void ResetForInvocation() => VerifiedItems.Clear();
    }

    private sealed class TrackingToolRegistry : IToolRegistry
    {
        public Dictionary<string, Func<string, CancellationToken, Task<string>>> BoundHandlers { get; } = [];

        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase) => [];
        public void RegisterTool(LopenToolDefinition tool) { }
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => [];
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler)
        {
            BoundHandlers[toolName] = handler;
            return true;
        }
    }

    private sealed class StubTaskStatusGate : Lopen.Llm.ITaskStatusGate
    {
        public TaskStatusGateResult Result { get; set; } = TaskStatusGateResult.Allowed();

        public TaskStatusGateResult ValidateCompletion(VerificationScope scope, string identifier) => Result;
    }

    private sealed class StubPlanManager : Lopen.Storage.IPlanManager
    {
        public List<(string Module, string TaskText, bool Completed)> Updates { get; } = [];

        public Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken cancellationToken = default)
        {
            Updates.Add((module, taskText, completed));
            return Task.FromResult(true);
        }

        public Task<string?> ReadPlanAsync(string module, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task WritePlanAsync(string module, string content, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> PlanExistsAsync(string module, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Lopen.Storage.PlanTask>> ReadTasksAsync(string module, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Lopen.Storage.PlanTask>>([]);
    }
}
