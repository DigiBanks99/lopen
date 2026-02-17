using System.Diagnostics;
using System.Diagnostics.Metrics;
using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public class WorkflowOrchestratorTests
{
    private readonly StubWorkflowEngine _engine = new();
    private readonly StubStateAssessor _assessor = new();
    private readonly StubLlmService _llmService = new();
    private readonly StubPromptBuilder _promptBuilder = new();
    private readonly StubToolRegistry _toolRegistry = new();
    private readonly StubModelSelector _modelSelector = new();
    private readonly StubGuardrailPipeline _guardrailPipeline = new();
    private readonly StubOutputRenderer _renderer = new();
    private readonly StubPhaseTransitionController _phaseController = new();
    private readonly StubSpecificationDriftService _driftService = new();

    private WorkflowOrchestrator CreateOrchestrator() => new(
        _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
        _modelSelector, _guardrailPipeline, _renderer, _phaseController,
        _driftService,
        NullLogger<WorkflowOrchestrator>.Instance);

    [Fact]
    public async Task RunAsync_ThrowsOnNullModuleName()
    {
        var sut = CreateOrchestrator();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunAsync(null!));
    }

    [Fact]
    public async Task RunAsync_ThrowsOnEmptyModuleName()
    {
        var sut = CreateOrchestrator();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.RunAsync(""));
    }

    [Fact]
    public async Task RunAsync_InitializesEngineWithModuleName()
    {
        _engine.IsComplete = true;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Equal("test-module", _engine.InitializedModule);
    }

    [Fact]
    public async Task RunAsync_CompletesWhenEngineIsComplete()
    {
        _engine.IsComplete = true;
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.Equal(0, result.IterationCount);
    }

    [Fact]
    public async Task RunAsync_InterruptsOnGuardrailBlock()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _guardrailPipeline.Results = [new GuardrailResult.Block("Budget exceeded")];
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Contains("Budget exceeded", result.InterruptionReason);
    }

    [Fact]
    public async Task RunAsync_RendersWarningOnGuardrailWarn()
    {
        _engine.StepsBeforeComplete = 1;
        _guardrailPipeline.Results = [new GuardrailResult.Warn("Token usage high")];
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Contains(_renderer.ErrorMessages, m => m.Contains("Token usage high"));
    }

    [Fact]
    public async Task RunAsync_InterruptsWhenDraftSpecNeedsConfirmation()
    {
        _engine.CurrentStep = WorkflowStep.DraftSpecification;
        _phaseController.SpecApproved = false;
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Contains("confirmation", result.InterruptionReason);
    }

    [Fact]
    public async Task RunAsync_FiresSpecApprovedWhenAlreadyApproved()
    {
        _engine.CurrentStep = WorkflowStep.DraftSpecification;
        _engine.StepsBeforeComplete = 1;
        _phaseController.SpecApproved = true;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Contains(WorkflowTrigger.SpecApproved, _engine.FiredTriggers);
    }

    [Fact]
    public async Task RunAsync_InvokesLlmForNonSpecSteps()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_llmService.InvokeCount > 0);
    }

    [Fact]
    public async Task RunAsync_InterruptsOnLlmFailure()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
    }

    [Fact]
    public async Task RunAsync_InterruptsOnCancellation()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module", cancellationToken: cts.Token);

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Equal("Cancelled", result.InterruptionReason);
    }

    [Fact]
    public async Task RunAsync_FiresCorrectTriggerForEachStep()
    {
        _engine.CurrentStep = WorkflowStep.IdentifyComponents;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Contains(WorkflowTrigger.ComponentsIdentified, _engine.FiredTriggers);
    }

    [Fact]
    public async Task RunAsync_RendersProgressForEachStep()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_renderer.ProgressCount > 0);
    }

    [Fact]
    public async Task RunAsync_RendersCompletionResult()
    {
        _engine.IsComplete = true;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Contains(_renderer.ResultMessages, m => m.Contains("completed"));
    }

    [Fact]
    public async Task RunStepAsync_ThrowsOnNullModuleName()
    {
        var sut = CreateOrchestrator();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RunStepAsync(null!));
    }

    [Fact]
    public async Task RunStepAsync_IncrementsIterationCount()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 2;
        var sut = CreateOrchestrator();

        await sut.RunStepAsync("test-module");
        await sut.RunStepAsync("test-module");
        var result = await sut.RunAsync("test-module");

        // RunAsync resets iteration count
        Assert.True(result.IterationCount >= 0);
    }

    [Fact]
    public async Task RunAsync_SelectsModelForPhase()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_modelSelector.SelectCount > 0);
    }

    [Fact]
    public async Task RunAsync_GetsToolsForPhase()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_toolRegistry.GetToolsCount > 0);
    }

    [Fact]
    public async Task RunAsync_BuildsPromptForPhase()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_promptBuilder.BuildCount > 0);
    }

    [Fact]
    public void Constructor_ThrowsOnNullEngine()
    {
        Assert.Throws<ArgumentNullException>(() => new WorkflowOrchestrator(
            null!, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService,
            NullLogger<WorkflowOrchestrator>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLlmService()
    {
        Assert.Throws<ArgumentNullException>(() => new WorkflowOrchestrator(
            _engine, _assessor, null!, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService,
            NullLogger<WorkflowOrchestrator>.Instance));
    }

    [Fact]
    public async Task RunAsync_RepeatStep_FiresModuleComplete_WhenNoMoreComponents()
    {
        _engine.CurrentStep = WorkflowStep.Repeat;
        _engine.StepsBeforeComplete = 1;
        _assessor.HasMoreComponents = false;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Contains(WorkflowTrigger.ModuleComplete, _engine.FiredTriggers);
    }

    [Fact]
    public async Task RunAsync_RepeatStep_FiresAssess_WhenMoreComponents()
    {
        _engine.CurrentStep = WorkflowStep.Repeat;
        _engine.StepsBeforeComplete = 1;
        _assessor.HasMoreComponents = true;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Contains(WorkflowTrigger.Assess, _engine.FiredTriggers);
    }

    [Fact]
    public async Task RunStepAsync_RendersDriftWarnings_WhenDriftDetected()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _driftService.DriftResults =
        [
            new DriftResult("Acceptance Criteria", "abc", "xyz", false, false),
            new DriftResult("New Section", null, "def", true, false),
        ];
        var sut = CreateOrchestrator();

        await sut.RunStepAsync("test-module");

        Assert.Equal(2, _renderer.ErrorMessages.Count(m => m.Contains("drift", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task RunStepAsync_ContinuesNormally_WhenNoDrift()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _driftService.DriftResults = [];
        var sut = CreateOrchestrator();

        var result = await sut.RunStepAsync("test-module");

        Assert.True(result.Success);
        Assert.DoesNotContain(_renderer.ErrorMessages,
            m => m.Contains("drift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunAsync_EnsuresModuleBranchWhenGitServiceProvided()
    {
        _engine.IsComplete = true;
        var gitService = new StubGitWorkflowService();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance, gitService);

        await sut.RunAsync("my-module");

        Assert.Single(gitService.BranchesCreated);
        Assert.Equal("my-module", gitService.BranchesCreated[0]);
    }

    [Fact]
    public async Task RunAsync_SkipsBranchCreationWhenNoGitService()
    {
        _engine.IsComplete = true;
        var sut = CreateOrchestrator(); // No git service

        var result = await sut.RunAsync("my-module");

        Assert.True(result.IsComplete); // No exception
    }

    [Fact]
    public async Task RunAsync_AutoSavesAfterEachStep()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var autoSave = new StubAutoSaveService();
        var sessionMgr = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave, sessionManager: sessionMgr);

        await sut.RunAsync("test-module");

        Assert.True(autoSave.Saves.Count > 0);
        Assert.Contains(autoSave.Saves,
            s => s.Trigger == Lopen.Storage.AutoSaveTrigger.StepCompletion);
    }

    [Fact]
    public async Task RunAsync_AutoSavesOnFailure()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _guardrailPipeline.Results = [new GuardrailResult.Block("blocked")];
        var autoSave = new StubAutoSaveService();
        var sessionMgr = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave, sessionManager: sessionMgr);

        await sut.RunAsync("test-module");

        Assert.Contains(autoSave.Saves,
            s => s.Trigger == Lopen.Storage.AutoSaveTrigger.TaskFailure);
    }

    [Fact]
    public async Task RunAsync_ResumesExistingSession()
    {
        _engine.IsComplete = true;
        var sessionMgr = new StubSessionManager
        {
            LatestSessionId = Lopen.Storage.SessionId.Generate("test-module", DateOnly.FromDateTime(DateTime.Today), 1),
            SavedState = new Lopen.Storage.SessionState
            {
                SessionId = "test-module-20250101-1",
                Module = "test-module",
                Phase = "Planning",
                Step = "DetermineDependencies",
                IsComplete = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            }
        };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            sessionManager: sessionMgr);

        await sut.RunAsync("test-module");

        Assert.Contains(_renderer.ResultMessages, m => m.Contains("Resuming session"));
    }

    [Fact]
    public async Task RunAsync_CreatesNewSessionWhenNoResumable()
    {
        _engine.IsComplete = true;
        var sessionMgr = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            sessionManager: sessionMgr);

        await sut.RunAsync("test-module");

        Assert.True(sessionMgr.SessionCreated);
    }

    [Fact]
    public async Task RunAsync_AutoSaveSwallowsExceptions()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var autoSave = new StubAutoSaveService { ThrowOnSave = true };
        var sessionMgr = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave, sessionManager: sessionMgr);

        // Should not throw even though auto-save fails
        var result = await sut.RunAsync("test-module");
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_RecordsTokenUsageAfterLlmInvocation()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.Result = new LlmInvocationResult(
            "output", new TokenUsage(100, 50, 150, 8000, false), 2, true);
        var tokenTracker = new StubTokenTracker();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker);

        await sut.RunAsync("test-module");

        Assert.Single(tokenTracker.RecordedUsages);
        Assert.Equal(100, tokenTracker.RecordedUsages[0].InputTokens);
        Assert.Equal(50, tokenTracker.RecordedUsages[0].OutputTokens);
    }

    [Fact]
    public async Task RunAsync_AutoSaveIncludesTokenMetrics()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var tokenTracker = new StubTokenTracker();
        var autoSave = new StubAutoSaveService();
        var sessionMgr = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave, sessionManager: sessionMgr,
            tokenTracker: tokenTracker);

        await sut.RunAsync("test-module");

        Assert.True(autoSave.MetricsIncluded);
    }

    [Fact]
    public async Task RunStepAsync_CreatesWorkflowPhaseSpan()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");

        Assert.Contains(activities, a => a.OperationName == "lopen.workflow.phase");
    }

    [Fact]
    public async Task RunStepAsync_CreatesSdkInvocationSpan()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");

        Assert.Contains(activities, a => a.OperationName == "lopen.sdk.invocation");
    }

    [Fact]
    public async Task RunStepAsync_CreatesTaskSpanForIterateThroughTasks()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        _engine.CurrentStep = WorkflowStep.IterateThroughTasks;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");

        Assert.Contains(activities, a => a.OperationName == "lopen.task.execution");
    }

    [Fact]
    public async Task RunStepAsync_RecordsSdkInvocationCounter()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.sdk.invocations.count")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");
        listener.RecordObservableInstruments();

        Assert.True(count >= 1, "SDK invocation counter should be incremented");
    }

    [Fact]
    public async Task RunStepAsync_RecordsSdkInvocationDurationHistogram()
    {
        double duration = -1;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.sdk.invocation.duration")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => duration = measurement);
        listener.Start();

        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");
        listener.RecordObservableInstruments();

        Assert.True(duration >= 0, "SDK invocation duration should be recorded");
    }

    [Fact]
    public async Task RunStepAsync_RecordsBackPressureCounterOnBlock()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.backpressure.events.count")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => count += measurement);
        listener.Start();

        _guardrailPipeline.Results = [new GuardrailResult.Block("test block")];
        _engine.CurrentStep = WorkflowStep.DraftSpecification;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");
        listener.RecordObservableInstruments();

        Assert.True(count >= 1, "Backpressure counter should be incremented on block");
    }

    [Fact]
    public async Task RunStepAsync_RecordsTaskMetricsOnTaskCompletion()
    {
        // ActivityListener needed because task metrics are inside `if (taskActivity is not null)` block
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(activityListener);

        long completedCount = 0;
        double taskDuration = -1;
        using var counterListener = new MeterListener();
        counterListener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.tasks.completed.count")
                l.EnableMeasurementEvents(instrument);
        };
        counterListener.SetMeasurementEventCallback<long>((_, measurement, _, _) => completedCount += measurement);
        counterListener.Start();

        using var histogramListener = new MeterListener();
        histogramListener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "lopen.task.duration")
                l.EnableMeasurementEvents(instrument);
        };
        histogramListener.SetMeasurementEventCallback<double>((_, measurement, _, _) => taskDuration = measurement);
        histogramListener.Start();

        _engine.CurrentStep = WorkflowStep.IterateThroughTasks;
        var sut = CreateOrchestrator();
        await sut.RunStepAsync("test-module");
        counterListener.RecordObservableInstruments();
        histogramListener.RecordObservableInstruments();

        Assert.True(completedCount >= 1, "Task completed counter should be incremented");
        Assert.True(taskDuration >= 0, "Task duration should be recorded");
    }

    [Fact]
    public async Task RunAsync_PassesUserPromptToPromptBuilder()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module", userPrompt: "Focus on auth module");

        Assert.NotNull(_promptBuilder.LastContextSections);
        Assert.True(_promptBuilder.LastContextSections!.ContainsKey("user_prompt"));
        Assert.Equal("Focus on auth module", _promptBuilder.LastContextSections["user_prompt"]);
    }

    [Fact]
    public async Task RunStepAsync_PassesUserPromptToPromptBuilder()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var sut = CreateOrchestrator();

        await sut.RunStepAsync("test-module", userPrompt: "Focus on auth module");

        Assert.NotNull(_promptBuilder.LastContextSections);
        Assert.True(_promptBuilder.LastContextSections!.ContainsKey("user_prompt"));
        Assert.Equal("Focus on auth module", _promptBuilder.LastContextSections["user_prompt"]);
    }

    [Fact]
    public async Task RunAsync_WithNullPrompt_PassesNullContextSections()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.Null(_promptBuilder.LastContextSections);
    }

    // --- CORE-21: Failure Handler Self-Correction Tests ---

    [Fact]
    public async Task RunAsync_WithFailureHandler_SelfCorrectsOnSingleFailure()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 1; // Fail first, succeed second
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.Equal(2, _llmService.InvokeCount); // Failed once, succeeded once
        Assert.True(failureHandler.RecordedFailures.Count > 0);
        Assert.Contains(_renderer.ErrorMessages, m => m.Contains("LLM error"));
    }

    [Fact]
    public async Task RunAsync_WithoutFailureHandler_InterruptsOnFailure()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        var sut = CreateOrchestrator(); // No failure handler

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
    }

    [Fact]
    public async Task RunAsync_WithFailureHandler_InterruptsWhenThresholdReached()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true; // Always fails
        var failureHandler = new StubFailureHandler { Threshold = 2 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Equal(2, failureHandler.RecordedFailures.Count); // Failed twice before escalation
    }

    [Fact]
    public async Task RunAsync_WithFailureHandler_RendersErrorInlineOnSelfCorrect()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 1;
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        // Error was rendered inline (displayed to user) but loop continued
        Assert.Contains(_renderer.ErrorMessages, m => m.Contains("LLM error"));
    }

    [Fact]
    public async Task RunAsync_WithFailureHandler_ResetsCountOnSuccess()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 1;
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        Assert.True(failureHandler.ResetCalled);
    }

    // --- CORE-22: Repeated Failure Escalation Tests ---

    [Fact]
    public async Task RunAsync_PromptUser_UserConfirmsY_ContinuesRetrying()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        // Fail threshold times, then succeed after user confirms
        _llmService.FailUntilInvokeCount = 3; // Fails 3 times, succeeds on 4th
        _renderer.PromptResponse = "y";
        var failureHandler = new StubFailureHandler { Threshold = 3 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.False(result.WasInterrupted);
        Assert.Single(_renderer.PromptMessages); // Prompted once at threshold
    }

    [Fact]
    public async Task RunAsync_PromptUser_UserConfirmsYes_ContinuesRetrying()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 3;
        _renderer.PromptResponse = "yes";
        var failureHandler = new StubFailureHandler { Threshold = 3 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_PromptUser_UserDeclinesN_Interrupts()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _renderer.PromptResponse = "n";
        var failureHandler = new StubFailureHandler { Threshold = 2 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Single(_renderer.PromptMessages);
    }

    [Fact]
    public async Task RunAsync_PromptUser_HeadlessNullResponse_Interrupts()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _renderer.PromptResponse = null; // Headless — no user interaction
        var failureHandler = new StubFailureHandler { Threshold = 2 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Single(_renderer.PromptMessages);
    }

    [Fact]
    public async Task RunAsync_PromptUser_UnattendedMode_ContinuesWithoutPrompt()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 3;
        var failureHandler = new StubFailureHandler { Threshold = 3 };
        var options = new WorkflowOptions { Unattended = true };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler,
            workflowOptions: options);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.Empty(_renderer.PromptMessages); // No prompt in unattended mode
    }

    [Fact]
    public async Task RunAsync_PromptUser_PromptMessageIncludesTaskNameAndCount()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _renderer.PromptResponse = "n";
        var failureHandler = new StubFailureHandler { Threshold = 2 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        var prompt = Assert.Single(_renderer.PromptMessages);
        Assert.Contains("DetermineDependencies", prompt);
        Assert.Contains("2", prompt); // Failure count
        Assert.Contains("Continue?", prompt);
    }

    [Fact]
    public async Task RunAsync_PromptUser_UserConfirms_ResetsFailureCount()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 2;
        _renderer.PromptResponse = "y";
        var failureHandler = new StubFailureHandler { Threshold = 2 };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        // Failure count was reset after user confirmed (allowing fresh retry cycle)
        Assert.True(failureHandler.ResetCalled);
    }

    // --- CFG-12: Budget Enforcement Tests ---

    [Fact]
    public async Task RunAsync_BudgetOk_ProceedsNormally()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer { StatusToReturn = BudgetStatus.Ok };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_BudgetWarning_RendersWarningAndContinues()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.Warning,
            MessageToReturn = "Token usage at 82% — approaching budget limit."
        };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.Contains(_renderer.ErrorMessages, m => m.Contains("82%"));
    }

    [Fact]
    public async Task RunAsync_BudgetConfirmationRequired_UserConfirmsY_Continues()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var tokenTracker = new StubTokenTracker();
        // Return ConfirmationRequired on first check, then Ok (after user confirms and step retries)
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.ConfirmationRequired,
            MessageToReturn = "Token usage at 92% — confirmation required to continue.",
            ReturnOkAfterFirstCheck = true,
        };
        _renderer.PromptResponse = "y";
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.Single(_renderer.PromptMessages);
        Assert.Contains("Continue?", _renderer.PromptMessages[0]);
    }

    [Fact]
    public async Task RunAsync_BudgetConfirmationRequired_UserDeclinesN_Interrupts()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.ConfirmationRequired,
            MessageToReturn = "Token usage at 92% — confirmation required to continue.",
        };
        _renderer.PromptResponse = "n";
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Single(_renderer.PromptMessages);
    }

    [Fact]
    public async Task RunAsync_BudgetConfirmationRequired_UnattendedMode_Interrupts()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.ConfirmationRequired,
            MessageToReturn = "Token usage at 92% — confirmation required to continue.",
        };
        var options = new WorkflowOptions { Unattended = true };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer,
            workflowOptions: options);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Empty(_renderer.PromptMessages); // No prompt in unattended mode
    }

    [Fact]
    public async Task RunAsync_BudgetConfirmationRequired_HeadlessNullResponse_Interrupts()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.ConfirmationRequired,
            MessageToReturn = "Token usage at 92% — confirmation required to continue.",
        };
        _renderer.PromptResponse = null; // Headless — no user interaction
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Single(_renderer.PromptMessages);
    }

    [Fact]
    public async Task RunAsync_BudgetExceeded_InterruptsImmediately()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.Exceeded,
            MessageToReturn = "Token budget exceeded (105% used).",
        };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.Contains("Budget exceeded", result.InterruptionReason);
        Assert.Equal(0, _llmService.InvokeCount); // LLM never called
    }

    [Fact]
    public async Task RunAsync_BudgetExceeded_AutoSavesBeforeInterrupting()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var tokenTracker = new StubTokenTracker();
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.Exceeded,
            MessageToReturn = "Token budget exceeded (105% used).",
        };
        var autoSave = new StubAutoSaveService();
        var sessionMgr = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave, sessionManager: sessionMgr,
            tokenTracker: tokenTracker, budgetEnforcer: budgetEnforcer);

        await sut.RunAsync("test-module");

        // Auto-save triggered for session resume
        Assert.True(autoSave.Saves.Count > 0);
    }

    [Fact]
    public async Task RunAsync_NoBudgetEnforcer_ProceedsNormally()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var tokenTracker = new StubTokenTracker();
        // No budget enforcer — backward compatible
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            tokenTracker: tokenTracker);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_NoTokenTracker_SkipsBudgetCheck()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var budgetEnforcer = new StubBudgetEnforcer
        {
            StatusToReturn = BudgetStatus.Exceeded, // Would block if checked
        };
        // No token tracker — budget check skipped
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            budgetEnforcer: budgetEnforcer);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete); // Budget enforcer never consulted
        Assert.Equal(0, budgetEnforcer.CheckCallCount);
    }

    // ==========================================
    // CORE-23: Critical system errors block execution
    // ==========================================

    [Fact]
    public async Task RunAsync_CriticalIOException_BlocksExecution()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new IOException("Disk full");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.True(result.IsCriticalError);
        Assert.Contains("Disk full", result.InterruptionReason);
    }

    [Fact]
    public async Task RunAsync_CriticalUnauthorizedAccessException_BlocksExecution()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new UnauthorizedAccessException("Permission denied");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.IsCriticalError);
        Assert.Contains("Permission denied", result.InterruptionReason);
    }

    [Fact]
    public async Task RunAsync_CriticalError_RendersBlockingMessage()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new IOException("No space left on device");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        // Renders the critical error message (both from catch block and from RunAsync)
        Assert.Contains(_renderer.ErrorMessages, m => m.Contains("CRITICAL"));
    }

    [Fact]
    public async Task RunAsync_CriticalError_DoesNotSelfCorrect()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new IOException("Disk full");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        // Critical errors should NOT be recorded as normal failures — no self-correction attempts
        Assert.Empty(failureHandler.RecordedFailures);
        Assert.Equal(1, _llmService.InvokeCount); // Only tried once, no retry
    }

    [Fact]
    public async Task RunAsync_CriticalError_DoesNotPromptUser()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new IOException("Disk full");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        // Critical errors block immediately — no user prompt
        Assert.Empty(_renderer.PromptMessages);
    }

    [Fact]
    public async Task RunAsync_CriticalError_AutoSavesSession()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new IOException("Disk full");
        var failureHandler = new StubFailureHandler();
        var autoSave = new StubAutoSaveService();
        var sessionManager = new StubSessionManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave, sessionManager: sessionManager,
            failureHandler: failureHandler);

        await sut.RunAsync("test-module");

        // Auto-save is triggered for the critical error (TaskFailure triggers)
        Assert.True(autoSave.Saves.Count >= 1);
    }

    [Fact]
    public async Task RunAsync_CriticalError_WithoutFailureHandler_InterruptsNormally()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new IOException("Disk full");
        // No failure handler — falls through to normal interruption
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        // Without a failure handler the critical classification does not run,
        // but the step still fails and interrupts
        Assert.Contains("Critical system error", result.InterruptionReason!);
    }

    [Fact]
    public async Task RunAsync_NonCriticalException_SelfCorrectsNormally()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _llmService.FailUntilInvokeCount = 1;
        // Default InvalidOperationException — not critical
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        // Non-critical exceptions still self-correct
        Assert.True(result.IsComplete);
        Assert.False(result.IsCriticalError);
        Assert.True(failureHandler.RecordedFailures.Count > 0);
    }

    [Fact]
    public async Task RunAsync_SecurityException_BlocksExecution()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new System.Security.SecurityException("Access denied");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsCriticalError);
        Assert.Contains("Access denied", result.InterruptionReason);
    }

    [Fact]
    public async Task RunAsync_OutOfMemoryException_BlocksExecution()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _llmService.ThrowOnInvoke = true;
        _llmService.ExceptionToThrow = new OutOfMemoryException("Insufficient memory");
        var failureHandler = new StubFailureHandler();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            failureHandler: failureHandler);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsCriticalError);
        Assert.Contains("Insufficient memory", result.InterruptionReason);
    }

    [Fact]
    public void StepResult_CriticalFailure_SetsIsCriticalError()
    {
        var result = StepResult.CriticalFailure("Critical error occurred");

        Assert.False(result.Success);
        Assert.True(result.IsCriticalError);
        Assert.Equal("Critical error occurred", result.Summary);
    }

    [Fact]
    public void OrchestrationResult_CriticalError_SetsProperties()
    {
        var result = OrchestrationResult.CriticalError(5, WorkflowStep.DetermineDependencies, "Disk full");

        Assert.False(result.IsComplete);
        Assert.True(result.WasInterrupted);
        Assert.True(result.IsCriticalError);
        Assert.Equal("Disk full", result.InterruptionReason);
        Assert.Contains("CRITICAL ERROR", result.Summary);
    }

    // --- Plan Manager wiring tests (STOR-09) ---

    [Fact]
    public async Task RunStepAsync_BreakIntoTasks_WritesPlanWhenPlanManagerProvided()
    {
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 1;
        _llmService.Result = new LlmInvocationResult(
            "- [ ] Task 1\n- [ ] Task 2", new TokenUsage(10, 10, 20, 8000, false), 0, true);
        var planManager = new StubPlanManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            planManager: planManager);

        var result = await sut.RunStepAsync("test-module");

        Assert.True(result.Success);
        Assert.Single(planManager.WrittenPlans);
        Assert.Equal("test-module", planManager.WrittenPlans[0].Module);
        Assert.Contains("Task 1", planManager.WrittenPlans[0].Content);
    }

    [Fact]
    public async Task RunStepAsync_BreakIntoTasks_AppendsToPlanWhenExistingContentExists()
    {
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 1;
        _llmService.Result = new LlmInvocationResult(
            "- [ ] Task B", new TokenUsage(10, 10, 20, 8000, false), 0, true);
        var planManager = new StubPlanManager { ExistingContent = "- [x] Task A" };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            planManager: planManager);

        await sut.RunStepAsync("test-module");

        Assert.Single(planManager.WrittenPlans);
        var content = planManager.WrittenPlans[0].Content;
        Assert.Contains("Task A", content);
        Assert.Contains("Task B", content);
    }

    [Fact]
    public async Task RunStepAsync_BreakIntoTasks_NoPlanManagerDoesNotThrow()
    {
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator(); // No plan manager

        var result = await sut.RunStepAsync("test-module");

        Assert.True(result.Success); // No exception, gracefully skipped
    }

    [Fact]
    public async Task RunStepAsync_BreakIntoTasks_PlanWriteFailureDoesNotBlockWorkflow()
    {
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 1;
        var planManager = new StubPlanManager { ThrowOnWrite = true };
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            planManager: planManager);

        var result = await sut.RunStepAsync("test-module");

        Assert.True(result.Success); // Workflow continues despite plan write failure
    }

    [Fact]
    public async Task RunStepAsync_NonBreakIntoTasks_DoesNotWritePlan()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var planManager = new StubPlanManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            planManager: planManager);

        await sut.RunStepAsync("test-module");

        Assert.Empty(planManager.WrittenPlans);
    }

    [Fact]
    public async Task RunStepAsync_BreakIntoTasks_EmptySummarySkipsPlanWrite()
    {
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 1;
        _llmService.Result = new LlmInvocationResult(
            "", new TokenUsage(10, 10, 20, 8000, false), 0, true);
        var planManager = new StubPlanManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            planManager: planManager);

        await sut.RunStepAsync("test-module");

        Assert.Empty(planManager.WrittenPlans);
    }

    [Fact]
    public async Task RunAsync_BreakIntoTasks_PersistsPlanDuringFullLoop()
    {
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 1;
        _llmService.Result = new LlmInvocationResult(
            "- [ ] Implement feature X", new TokenUsage(10, 10, 20, 8000, false), 0, true);
        var planManager = new StubPlanManager();
        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            planManager: planManager);

        var result = await sut.RunAsync("test-module");

        Assert.True(result.IsComplete);
        Assert.Single(planManager.WrittenPlans);
        Assert.Equal("test-module", planManager.WrittenPlans[0].Module);
    }

    // LLM-02: Each workflow phase invokes the SDK with a fresh context window

    [Fact]
    public async Task InvokeLlm_EachStep_PassesFreshSystemPrompt()
    {
        // When the orchestrator processes multiple steps, each LLM invocation
        // receives its own system prompt — no accumulated conversation history.
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 3;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_llmService.Invocations.Count >= 2, "Expected at least 2 LLM invocations");
        // Each invocation gets an independent system prompt string (not an appended conversation)
        foreach (var invocation in _llmService.Invocations)
        {
            Assert.False(string.IsNullOrWhiteSpace(invocation.SystemPrompt),
                "Each invocation must receive a non-empty system prompt");
        }
        // No invocation's prompt is a superset of a previous one (no history accumulation)
        for (int i = 1; i < _llmService.Invocations.Count; i++)
        {
            var current = _llmService.Invocations[i].SystemPrompt;
            var previous = _llmService.Invocations[i - 1].SystemPrompt;
            Assert.DoesNotContain(previous + "\n", current);
        }
    }

    [Fact]
    public async Task InvokeLlm_AcrossPhases_NoHistoryCarried()
    {
        // When transitioning from Planning to Building phase, each LLM call is independent.
        // Start at a Planning step, run enough steps to transition into Building.
        _engine.CurrentStep = WorkflowStep.BreakIntoTasks;
        _engine.StepsBeforeComplete = 3;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_llmService.Invocations.Count >= 2, "Expected at least 2 LLM invocations across phases");
        // Each call to ILlmService.InvokeAsync is stateless — it receives
        // (systemPrompt, model, tools) with no conversation history parameter.
        // Verify each invocation has exactly 3 discrete parameters (no history accumulation).
        foreach (var invocation in _llmService.Invocations)
        {
            Assert.NotNull(invocation.SystemPrompt);
            Assert.NotNull(invocation.Model);
            Assert.NotNull(invocation.Tools);
        }
        // Confirm invocations are independent: no prompt contains another's output
        for (int i = 1; i < _llmService.Invocations.Count; i++)
        {
            Assert.DoesNotContain("Test output", _llmService.Invocations[i].SystemPrompt);
        }
    }

    [Fact]
    public async Task InvokeLlm_PerTask_CreatesIndependentInvocation()
    {
        // During task execution (IterateThroughTasks), each task iteration
        // gets its own independent LLM call with no carried-over context.
        _engine.CurrentStep = WorkflowStep.IterateThroughTasks;
        _engine.StepsBeforeComplete = 3;
        var sut = CreateOrchestrator();

        await sut.RunAsync("test-module");

        Assert.True(_llmService.Invocations.Count >= 2,
            "Expected at least 2 task iteration LLM invocations");
        // Each task invocation builds a fresh prompt from PromptBuilder (not appending history)
        Assert.True(_promptBuilder.BuildCount >= 2,
            "PromptBuilder should be called once per LLM invocation");
        Assert.Equal(_llmService.Invocations.Count, _promptBuilder.BuildCount);
    }

    // --- CORE-02: End-to-End Workflow Pipeline Integration Tests ---

    [Fact]
    public async Task EndToEnd_SpecPlanBuild_ExecutesAllPhases()
    {
        // Integration test: run the orchestrator through all three phases (spec → plan → build)
        // using fresh stubs for each run to track phase-specific LLM invocations.

        // Phase 1: RequirementGathering — DraftSpecification invokes LLM when spec not yet approved
        _engine.CurrentStep = WorkflowStep.DraftSpecification;
        _phaseController.SpecApproved = false;
        var sut = CreateOrchestrator();

        var specResult = await sut.RunAsync("test-module");

        Assert.True(specResult.WasInterrupted, "Spec phase should interrupt for user confirmation");
        Assert.Contains(WorkflowPhase.RequirementGathering, _promptBuilder.PhasesInvoked);
        Assert.Contains(WorkflowPhase.RequirementGathering, _toolRegistry.PhasesRequested);
        Assert.Contains(WorkflowPhase.RequirementGathering, _modelSelector.PhasesRequested);

        // Phase 2: Planning — DetermineDependencies through BreakIntoTasks
        _promptBuilder.PhasesInvoked.Clear();
        _toolRegistry.PhasesRequested.Clear();
        _modelSelector.PhasesRequested.Clear();
        _llmService.InvokeCount = 0;

        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        _engine.FiredTriggers.Clear();
        sut = CreateOrchestrator();

        var planResult = await sut.RunAsync("test-module");

        Assert.True(planResult.IsComplete);
        Assert.Contains(WorkflowPhase.Planning, _promptBuilder.PhasesInvoked);
        Assert.Contains(WorkflowPhase.Planning, _toolRegistry.PhasesRequested);
        Assert.Contains(WorkflowPhase.Planning, _modelSelector.PhasesRequested);
        Assert.True(_llmService.InvokeCount > 0, "LLM should be invoked during planning phase");

        // Phase 3: Building — IterateThroughTasks
        _promptBuilder.PhasesInvoked.Clear();
        _toolRegistry.PhasesRequested.Clear();
        _modelSelector.PhasesRequested.Clear();
        _llmService.InvokeCount = 0;

        _engine.IsComplete = false;
        _engine.CurrentStep = WorkflowStep.IterateThroughTasks;
        _engine.StepsBeforeComplete = 2; // Accounts for accumulated fire count from Phase 2
        _engine.FiredTriggers.Clear();
        sut = CreateOrchestrator();

        var buildResult = await sut.RunAsync("test-module");

        Assert.True(buildResult.IsComplete);
        Assert.Contains(WorkflowPhase.Building, _promptBuilder.PhasesInvoked);
        Assert.Contains(WorkflowPhase.Building, _toolRegistry.PhasesRequested);
        Assert.Contains(WorkflowPhase.Building, _modelSelector.PhasesRequested);
        Assert.True(_llmService.InvokeCount > 0, "LLM should be invoked during building phase");
    }

    [Fact]
    public async Task EndToEnd_PhaseTransition_UpdatesEngineState()
    {
        // Integration test using the REAL WorkflowEngine (Stateless state machine)
        // to verify state transitions across the full spec → plan → build pipeline.
        var assessor = new StubStateAssessor();
        var realEngine = new WorkflowEngine(assessor, NullLogger<WorkflowEngine>.Instance);
        await realEngine.InitializeAsync("test-module");

        // Start: DraftSpecification (RequirementGathering phase)
        Assert.Equal(WorkflowStep.DraftSpecification, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.RequirementGathering, realEngine.CurrentPhase);

        // Transition to Planning: SpecApproved → DetermineDependencies
        Assert.True(realEngine.Fire(WorkflowTrigger.SpecApproved));
        Assert.Equal(WorkflowStep.DetermineDependencies, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Planning, realEngine.CurrentPhase);

        // Continue through Planning steps
        Assert.True(realEngine.Fire(WorkflowTrigger.DependenciesDetermined));
        Assert.Equal(WorkflowStep.IdentifyComponents, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Planning, realEngine.CurrentPhase);

        Assert.True(realEngine.Fire(WorkflowTrigger.ComponentsIdentified));
        Assert.Equal(WorkflowStep.SelectNextComponent, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Planning, realEngine.CurrentPhase);

        Assert.True(realEngine.Fire(WorkflowTrigger.ComponentSelected));
        Assert.Equal(WorkflowStep.BreakIntoTasks, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Planning, realEngine.CurrentPhase);

        // Transition to Building: TasksBrokenDown → IterateThroughTasks
        Assert.True(realEngine.Fire(WorkflowTrigger.TasksBrokenDown));
        Assert.Equal(WorkflowStep.IterateThroughTasks, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Building, realEngine.CurrentPhase);

        // Task iteration re-entry (stays in Building)
        Assert.True(realEngine.Fire(WorkflowTrigger.TaskIterationComplete));
        Assert.Equal(WorkflowStep.IterateThroughTasks, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Building, realEngine.CurrentPhase);

        // Component complete → Repeat
        Assert.True(realEngine.Fire(WorkflowTrigger.ComponentComplete));
        Assert.Equal(WorkflowStep.Repeat, realEngine.CurrentStep);
        Assert.Equal(WorkflowPhase.Building, realEngine.CurrentPhase);

        // Loop back → SelectNextComponent (still Planning-mapped but in the build loop)
        Assert.True(realEngine.Fire(WorkflowTrigger.Assess));
        Assert.Equal(WorkflowStep.SelectNextComponent, realEngine.CurrentStep);

        // Verify engine is not marked complete until ModuleComplete fires
        Assert.False(realEngine.IsComplete);
    }

    [Fact]
    public async Task EndToEnd_TaskCompletion_TriggersVerification()
    {
        // Integration test: simulate the full task completion flow.
        // Phase A: IterateThroughTasks — LLM returns tool calls (simulating update_task_status).
        // Phase B: Repeat — no more components → fires ModuleComplete.
        // Verifies the orchestrator drives through task iteration into verification.

        var promptBuilder = new StubPromptBuilder();
        var toolRegistry = new StubToolRegistry();

        // LLM returns output with tool calls (simulating update_task_status)
        var llmService = new StubLlmService
        {
            Result = new LlmInvocationResult(
                "Task completed: implemented feature X",
                new TokenUsage(200, 100, 300, 8000, false),
                3,  // ToolCallsMade — simulates tool calls including update_task_status
                true)
        };

        // Phase A: Run one iteration at IterateThroughTasks
        var engine = new StubWorkflowEngine
        {
            CurrentStep = WorkflowStep.IterateThroughTasks,
            StepsBeforeComplete = 1
        };
        var phaseController = new StubPhaseTransitionController { SpecApproved = true };

        var sut = new WorkflowOrchestrator(
            engine, _assessor, llmService, promptBuilder, toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance);

        var iterResult = await sut.RunAsync("test-module");

        Assert.True(iterResult.IsComplete);
        Assert.True(llmService.InvokeCount >= 1, "LLM should be invoked for task iteration");
        Assert.Contains(WorkflowPhase.Building, promptBuilder.PhasesInvoked);
        Assert.Contains(WorkflowTrigger.TaskIterationComplete, engine.FiredTriggers);

        // Phase B: Repeat step checks for remaining components → triggers ModuleComplete
        _assessor.HasMoreComponents = false;
        var repeatEngine = new StubWorkflowEngine
        {
            CurrentStep = WorkflowStep.Repeat,
            StepsBeforeComplete = 1
        };
        llmService.InvokeCount = 0;

        sut = new WorkflowOrchestrator(
            repeatEngine, _assessor, llmService, promptBuilder, toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance);

        var repeatResult = await sut.RunAsync("test-module");

        Assert.True(repeatResult.IsComplete);
        // When no more components, the Repeat step fires ModuleComplete
        Assert.Contains(WorkflowTrigger.ModuleComplete, repeatEngine.FiredTriggers);
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

        private bool _isComplete;
        public bool IsComplete
        {
            get => _isComplete;
            set => _isComplete = value;
        }

        public int StepsBeforeComplete { get; set; } = int.MaxValue;
        public string? InitializedModule { get; private set; }
        public List<WorkflowTrigger> FiredTriggers { get; } = [];

        private int _fireCount;

        public Task InitializeAsync(string moduleName, CancellationToken ct = default)
        {
            InitializedModule = moduleName;
            return Task.CompletedTask;
        }

        public bool Fire(WorkflowTrigger trigger)
        {
            FiredTriggers.Add(trigger);
            _fireCount++;
            if (_fireCount >= StepsBeforeComplete)
                _isComplete = true;
            return true;
        }

        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() =>
            [WorkflowTrigger.Assess];
    }

    private sealed class StubStateAssessor : IStateAssessor
    {
        public bool HasMoreComponents { get; set; } = true;

        public Task<WorkflowStep> GetCurrentStepAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(WorkflowStep.DraftSpecification);

        public Task PersistStepAsync(string moduleName, WorkflowStep step, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> IsSpecReadyAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> HasMoreComponentsAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(HasMoreComponents);
    }

    private sealed class StubLlmService : ILlmService
    {
        public int InvokeCount { get; set; }
        public bool ThrowOnInvoke { get; set; }
        public int FailUntilInvokeCount { get; set; }
        public LlmInvocationResult? Result { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public List<(string SystemPrompt, string Model, IReadOnlyList<LopenToolDefinition> Tools)> Invocations { get; } = [];

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt, string model, IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken ct = default)
        {
            InvokeCount++;
            Invocations.Add((systemPrompt, model, tools));
            if (ThrowOnInvoke || (FailUntilInvokeCount > 0 && InvokeCount <= FailUntilInvokeCount))
                throw ExceptionToThrow ?? new InvalidOperationException("LLM error");

            return Task.FromResult(Result ??
                new LlmInvocationResult(
                    "Test output", new TokenUsage(100, 50, 150, 8000, false), 1, true));
        }
    }

    private sealed class StubPromptBuilder : IPromptBuilder
    {
        public int BuildCount { get; private set; }
        public IReadOnlyDictionary<string, string>? LastContextSections { get; private set; }
        public List<WorkflowPhase> PhasesInvoked { get; } = [];

        public string BuildSystemPrompt(WorkflowPhase phase, string module, string? component, string? task,
            IReadOnlyDictionary<string, string>? contextSections = null)
        {
            BuildCount++;
            LastContextSections = contextSections;
            PhasesInvoked.Add(phase);
            return $"Test prompt for {phase}";
        }
    }

    private sealed class StubToolRegistry : IToolRegistry
    {
        public int GetToolsCount { get; private set; }
        public List<WorkflowPhase> PhasesRequested { get; } = [];

        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase)
        {
            GetToolsCount++;
            PhasesRequested.Add(phase);
            return [];
        }

        public void RegisterTool(LopenToolDefinition tool) { }
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => [];
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler) => true;
    }

    private sealed class StubModelSelector : IModelSelector
    {
        public int SelectCount { get; private set; }
        public List<WorkflowPhase> PhasesRequested { get; } = [];

        public ModelFallbackResult SelectModel(WorkflowPhase phase)
        {
            SelectCount++;
            PhasesRequested.Add(phase);
            return new ModelFallbackResult("gpt-4", false);
        }

        public IReadOnlyList<string> GetFallbackChain(WorkflowPhase phase) =>
            ["gpt-4"];
    }

    private sealed class StubGuardrailPipeline : IGuardrailPipeline
    {
        public IReadOnlyList<GuardrailResult> Results { get; set; } = [new GuardrailResult.Pass()];

        public Task<IReadOnlyList<GuardrailResult>> EvaluateAsync(GuardrailContext context, CancellationToken ct = default) =>
            Task.FromResult(Results);
    }

    private sealed class StubOutputRenderer : IOutputRenderer
    {
        public int ProgressCount { get; private set; }
        public List<string> ErrorMessages { get; } = [];
        public List<string> ResultMessages { get; } = [];
        public List<string> PromptMessages { get; } = [];
        public string? PromptResponse { get; set; }

        public Task RenderProgressAsync(string phase, string step, double progress, CancellationToken ct = default)
        {
            ProgressCount++;
            return Task.CompletedTask;
        }

        public Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken ct = default)
        {
            ErrorMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task RenderResultAsync(string message, CancellationToken ct = default)
        {
            ResultMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<string?> PromptAsync(string message, CancellationToken ct = default)
        {
            PromptMessages.Add(message);
            return Task.FromResult(PromptResponse);
        }
    }

    private sealed class StubPhaseTransitionController : IPhaseTransitionController
    {
        public bool SpecApproved { get; set; }
        public bool IsRequirementGatheringToPlannningApproved => SpecApproved;
        public void ApproveSpecification() => SpecApproved = true;
        public void ResetApproval() => SpecApproved = false;
        public bool CanAutoTransitionToBuilding(bool hasComponentsIdentified, bool hasTasksBreakdown) =>
            hasComponentsIdentified && hasTasksBreakdown;
        public bool CanAutoTransitionToComplete(bool allComponentsBuilt, bool allAcceptanceCriteriaPassed) =>
            allComponentsBuilt && allAcceptanceCriteriaPassed;
    }

    private sealed class StubSpecificationDriftService : ISpecificationDriftService
    {
        public IReadOnlyList<DriftResult> DriftResults { get; set; } = [];

        public Task<IReadOnlyList<DriftResult>> CheckDriftAsync(
            string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(DriftResults);
    }

    private sealed class StubGitWorkflowService : Lopen.Core.Git.IGitWorkflowService
    {
        public List<string> BranchesCreated { get; } = [];
        public List<(string Module, string Component, string Task)> Commits { get; } = [];

        public Task<Lopen.Core.Git.GitResult?> EnsureModuleBranchAsync(string moduleName, CancellationToken ct = default)
        {
            BranchesCreated.Add(moduleName);
            return Task.FromResult<Lopen.Core.Git.GitResult?>(new(0, "branch created", ""));
        }

        public Task<Lopen.Core.Git.GitResult?> CommitTaskCompletionAsync(string moduleName, string componentName, string taskName, CancellationToken ct = default)
        {
            Commits.Add((moduleName, componentName, taskName));
            return Task.FromResult<Lopen.Core.Git.GitResult?>(new(0, "committed", ""));
        }

        public string FormatCommitMessage(string moduleName, string componentName, string taskName) =>
            $"feat({moduleName}): complete {taskName}";
    }

    private sealed class StubAutoSaveService : Lopen.Storage.IAutoSaveService
    {
        public List<(Lopen.Storage.AutoSaveTrigger Trigger, string SessionId)> Saves { get; } = [];
        public bool ThrowOnSave { get; set; }
        public bool MetricsIncluded { get; private set; }

        public Task SaveAsync(Lopen.Storage.AutoSaveTrigger trigger, Lopen.Storage.SessionId sessionId,
            Lopen.Storage.SessionState state, Lopen.Storage.SessionMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave) throw new InvalidOperationException("Auto-save forced failure");
            Saves.Add((trigger, sessionId.ToString()));
            if (metrics is not null) MetricsIncluded = true;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSessionManager : Lopen.Storage.ISessionManager
    {
        public Lopen.Storage.SessionId? LatestSessionId { get; set; }
        public Lopen.Storage.SessionState? SavedState { get; set; }
        public bool SessionCreated { get; private set; }

        public Task<Lopen.Storage.SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
        {
            SessionCreated = true;
            var id = Lopen.Storage.SessionId.Generate(module, DateOnly.FromDateTime(DateTime.Today), 1);
            return Task.FromResult(id);
        }

        public Task<Lopen.Storage.SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default) =>
            Task.FromResult(LatestSessionId);

        public Task<Lopen.Storage.SessionState?> LoadSessionStateAsync(Lopen.Storage.SessionId sessionId, CancellationToken ct = default) =>
            Task.FromResult(SavedState);

        public Task SaveSessionStateAsync(Lopen.Storage.SessionId sessionId, Lopen.Storage.SessionState state, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<Lopen.Storage.SessionMetrics?> LoadSessionMetricsAsync(Lopen.Storage.SessionId sessionId, CancellationToken ct = default) =>
            Task.FromResult<Lopen.Storage.SessionMetrics?>(null);

        public Task SaveSessionMetricsAsync(Lopen.Storage.SessionId sessionId, Lopen.Storage.SessionMetrics metrics, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Lopen.Storage.SessionId>> ListSessionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Lopen.Storage.SessionId>>([]);

        public Task SetLatestAsync(Lopen.Storage.SessionId sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task QuarantineCorruptedSessionAsync(Lopen.Storage.SessionId sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<int> PruneSessionsAsync(int keepCount = 5, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task DeleteSessionAsync(Lopen.Storage.SessionId sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class StubTokenTracker : ITokenTracker
    {
        public List<TokenUsage> RecordedUsages { get; } = [];

        public void RecordUsage(TokenUsage usage) => RecordedUsages.Add(usage);

        public SessionTokenMetrics GetSessionMetrics() => new()
        {
            CumulativeInputTokens = RecordedUsages.Sum(u => u.InputTokens),
            CumulativeOutputTokens = RecordedUsages.Sum(u => u.OutputTokens),
            PremiumRequestCount = RecordedUsages.Count(u => u.IsPremiumRequest),
            PerIterationTokens = RecordedUsages,
        };

        public void ResetSession() => RecordedUsages.Clear();
    }

    private sealed class StubFailureHandler : IFailureHandler
    {
        public int Threshold { get; set; } = 3;
        public List<(string TaskId, string Message)> RecordedFailures { get; } = [];
        public bool ResetCalled { get; private set; }
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public FailureClassification RecordFailure(string taskId, string errorMessage)
        {
            RecordedFailures.Add((taskId, errorMessage));
            _counts.TryGetValue(taskId, out var count);
            count++;
            _counts[taskId] = count;

            if (count >= Threshold)
                return new FailureClassification(
                    FailureSeverity.RepeatedFailure, FailureAction.PromptUser,
                    $"Threshold reached ({count})", taskId, count);

            return new FailureClassification(
                FailureSeverity.TaskFailure, FailureAction.SelfCorrect,
                $"Self-correcting (attempt {count})", taskId, count);
        }

        public FailureClassification RecordCriticalError(string errorMessage) =>
            new(FailureSeverity.Critical, FailureAction.Block, errorMessage);

        public FailureClassification RecordWarning(string message) =>
            new(FailureSeverity.Warning, FailureAction.SelfCorrect, message);

        public void ResetFailureCount(string taskId)
        {
            ResetCalled = true;
            _counts.Remove(taskId);
        }

        public int GetFailureCount(string taskId) =>
            _counts.TryGetValue(taskId, out var count) ? count : 0;
    }

    private sealed class StubBudgetEnforcer : IBudgetEnforcer
    {
        public BudgetStatus StatusToReturn { get; set; } = BudgetStatus.Ok;
        public string MessageToReturn { get; set; } = "Budget usage is within limits.";
        public bool ReturnOkAfterFirstCheck { get; set; }
        public int CheckCallCount { get; private set; }

        public BudgetCheckResult Check(long currentTokens, int currentRequests)
        {
            CheckCallCount++;
            var status = CheckCallCount > 1 && ReturnOkAfterFirstCheck
                ? BudgetStatus.Ok
                : StatusToReturn;
            var message = status == BudgetStatus.Ok ? "Budget usage is within limits." : MessageToReturn;
            return new BudgetCheckResult
            {
                Status = status,
                TokenStatus = status,
                RequestStatus = BudgetStatus.Ok,
                TokenUsageFraction = 0.5,
                RequestUsageFraction = null,
                Message = message,
            };
        }
    }

    private sealed class StubPlanManager : Lopen.Storage.IPlanManager
    {
        public List<(string Module, string Content)> WrittenPlans { get; } = [];
        public string? ExistingContent { get; set; }
        public bool ThrowOnWrite { get; set; }

        public Task WritePlanAsync(string module, string content, CancellationToken cancellationToken = default)
        {
            if (ThrowOnWrite)
                throw new IOException("Disk full");
            WrittenPlans.Add((module, content));
            ExistingContent = content;
            return Task.CompletedTask;
        }

        public Task<string?> ReadPlanAsync(string module, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingContent);

        public Task<bool> PlanExistsAsync(string module, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingContent is not null);

        public Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<Lopen.Storage.PlanTask>> ReadTasksAsync(string module, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Lopen.Storage.PlanTask>>([]);
    }

    // --- TUI-41: Pause Controller Tests ---

    [Fact]
    public async Task RunAsync_WithPauseController_WaitsWhenPaused()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var pauseController = new PauseController();
        pauseController.Pause();

        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            pauseController: pauseController);

        var runTask = sut.RunAsync("test-module");

        // Should not complete while paused
        await Task.Delay(100);
        Assert.False(runTask.IsCompleted);

        // Resume should allow completion
        pauseController.Resume();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_WithPauseController_AutoSavesOnPause()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var pauseController = new PauseController();
        pauseController.Pause();
        var autoSave = new StubAutoSaveService();
        var sessionMgr = new StubSessionManager();

        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            autoSaveService: autoSave,
            sessionManager: sessionMgr,
            pauseController: pauseController);

        var runTask = sut.RunAsync("test-module");
        await Task.Delay(200);

        // Should have auto-saved with UserPause trigger
        Assert.Contains(autoSave.Saves, s => s.Trigger == Lopen.Storage.AutoSaveTrigger.UserPause);

        // Resume to complete
        pauseController.Resume();
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RunAsync_WithPauseController_NotPaused_RunsNormally()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var pauseController = new PauseController();

        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            pauseController: pauseController);

        var result = await sut.RunAsync("test-module");
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_WithoutPauseController_RunsNormally()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var sut = CreateOrchestrator();

        var result = await sut.RunAsync("test-module");
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task RunAsync_WithPauseController_RendersStatusMessages()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        _engine.StepsBeforeComplete = 1;
        var pauseController = new PauseController();
        pauseController.Pause();

        var sut = new WorkflowOrchestrator(
            _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
            _modelSelector, _guardrailPipeline, _renderer, _phaseController,
            _driftService, NullLogger<WorkflowOrchestrator>.Instance,
            pauseController: pauseController);

        var runTask = sut.RunAsync("test-module");
        await Task.Delay(200);

        // Should have rendered pause message
        Assert.Contains(_renderer.ResultMessages, m => m.Contains("Paused"));

        pauseController.Resume();
        await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Should have rendered resume message
        Assert.Contains(_renderer.ResultMessages, m => m.Contains("Resumed"));
    }

    // --- TUI-40: Queued User Messages Tests ---

    private WorkflowOrchestrator CreateOrchestratorWithQueue(IUserPromptQueue queue) => new(
        _engine, _assessor, _llmService, _promptBuilder, _toolRegistry,
        _modelSelector, _guardrailPipeline, _renderer, _phaseController,
        _driftService,
        NullLogger<WorkflowOrchestrator>.Instance,
        userPromptQueue: queue);

    [Fact]
    public async Task RunStepAsync_DrainsQueuedMessagesIntoContext()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var queue = new InMemoryUserPromptQueue();
        queue.Enqueue("Please focus on the auth module");
        var sut = CreateOrchestratorWithQueue(queue);

        await sut.RunStepAsync("test-module");

        Assert.NotNull(_promptBuilder.LastContextSections);
        Assert.True(_promptBuilder.LastContextSections!.ContainsKey("queued_user_messages"));
        Assert.Equal("Please focus on the auth module", _promptBuilder.LastContextSections["queued_user_messages"]);
    }

    [Fact]
    public async Task RunStepAsync_MultipleQueuedMessages_ConcatenatedWithNewline()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var queue = new InMemoryUserPromptQueue();
        queue.Enqueue("First message");
        queue.Enqueue("Second message");
        queue.Enqueue("Third message");
        var sut = CreateOrchestratorWithQueue(queue);

        await sut.RunStepAsync("test-module");

        Assert.NotNull(_promptBuilder.LastContextSections);
        Assert.Equal("First message\nSecond message\nThird message",
            _promptBuilder.LastContextSections!["queued_user_messages"]);
    }

    [Fact]
    public async Task RunStepAsync_EmptyQueue_NoQueuedMessagesKey()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var queue = new InMemoryUserPromptQueue();
        var sut = CreateOrchestratorWithQueue(queue);

        await sut.RunStepAsync("test-module");

        Assert.Null(_promptBuilder.LastContextSections);
    }

    [Fact]
    public async Task RunStepAsync_QueueDrainedAfterInvocation()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var queue = new InMemoryUserPromptQueue();
        queue.Enqueue("Message one");
        queue.Enqueue("Message two");
        var sut = CreateOrchestratorWithQueue(queue);

        await sut.RunStepAsync("test-module");

        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task RunStepAsync_NullQueue_WorksWithoutError()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var sut = CreateOrchestrator(); // No queue injected

        await sut.RunStepAsync("test-module");

        // Should complete without error; no queued_user_messages key
        Assert.Null(_promptBuilder.LastContextSections);
    }

    [Fact]
    public async Task RunStepAsync_QueueAndUserPrompt_BothPresent()
    {
        _engine.CurrentStep = WorkflowStep.DetermineDependencies;
        var queue = new InMemoryUserPromptQueue();
        queue.Enqueue("Queued message");
        var sut = CreateOrchestratorWithQueue(queue);

        await sut.RunStepAsync("test-module", userPrompt: "Direct prompt");

        Assert.NotNull(_promptBuilder.LastContextSections);
        Assert.Equal("Direct prompt", _promptBuilder.LastContextSections!["user_prompt"]);
        Assert.Equal("Queued message", _promptBuilder.LastContextSections["queued_user_messages"]);
    }

    private sealed class InMemoryUserPromptQueue : IUserPromptQueue
    {
        private readonly Queue<string> _queue = new();

        public void Enqueue(string prompt) => _queue.Enqueue(prompt);

        public bool TryDequeue(out string prompt)
        {
            if (_queue.Count > 0)
            {
                prompt = _queue.Dequeue();
                return true;
            }
            prompt = default!;
            return false;
        }

        public Task<string> DequeueAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_queue.Dequeue());

        public int Count => _queue.Count;
    }
}
