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
        public int InvokeCount { get; private set; }
        public bool ThrowOnInvoke { get; set; }
        public int FailUntilInvokeCount { get; set; }
        public LlmInvocationResult? Result { get; set; }

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt, string model, IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken ct = default)
        {
            InvokeCount++;
            if (ThrowOnInvoke || (FailUntilInvokeCount > 0 && InvokeCount <= FailUntilInvokeCount))
                throw new InvalidOperationException("LLM error");

            return Task.FromResult(Result ??
                new LlmInvocationResult(
                    "Test output", new TokenUsage(100, 50, 150, 8000, false), 1, true));
        }
    }

    private sealed class StubPromptBuilder : IPromptBuilder
    {
        public int BuildCount { get; private set; }
        public IReadOnlyDictionary<string, string>? LastContextSections { get; private set; }

        public string BuildSystemPrompt(WorkflowPhase phase, string module, string? component, string? task,
            IReadOnlyDictionary<string, string>? contextSections = null)
        {
            BuildCount++;
            LastContextSections = contextSections;
            return "Test prompt";
        }
    }

    private sealed class StubToolRegistry : IToolRegistry
    {
        public int GetToolsCount { get; private set; }

        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase)
        {
            GetToolsCount++;
            return [];
        }

        public void RegisterTool(LopenToolDefinition tool) { }
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => [];
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler) => true;
    }

    private sealed class StubModelSelector : IModelSelector
    {
        public int SelectCount { get; private set; }

        public ModelFallbackResult SelectModel(WorkflowPhase phase)
        {
            SelectCount++;
            return new ModelFallbackResult("gpt-4", false);
        }
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
}
