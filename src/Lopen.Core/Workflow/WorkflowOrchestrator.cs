using System.Diagnostics;
using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Llm;
using Lopen.Otel;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Drives the orchestration loop: assess → guardrails → invoke LLM → transition → save.
/// The engine handles state transitions; this class drives the outer loop.
/// </summary>
internal sealed class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IWorkflowEngine _engine;
    private readonly IStateAssessor _assessor;
    private readonly ILlmService _llmService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IToolRegistry _toolRegistry;
    private readonly IModelSelector _modelSelector;
    private readonly IGuardrailPipeline _guardrailPipeline;
    private readonly IOutputRenderer _renderer;
    private readonly IPhaseTransitionController _phaseController;
    private readonly ISpecificationDriftService _driftService;
    private readonly IGitWorkflowService? _gitWorkflowService;
    private readonly IAutoSaveService? _autoSaveService;
    private readonly ISessionManager? _sessionManager;
    private readonly ITokenTracker? _tokenTracker;
    private readonly IFailureHandler? _failureHandler;
    private readonly IBudgetEnforcer? _budgetEnforcer;
    private readonly IPlanManager? _planManager;
    private readonly IPauseController? _pauseController;
    private readonly WorkflowOptions? _workflowOptions;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    private int _iterationCount;
    private SessionId? _sessionId;
    private string? _userPrompt;

    public WorkflowOrchestrator(
        IWorkflowEngine engine,
        IStateAssessor assessor,
        ILlmService llmService,
        IPromptBuilder promptBuilder,
        IToolRegistry toolRegistry,
        IModelSelector modelSelector,
        IGuardrailPipeline guardrailPipeline,
        IOutputRenderer renderer,
        IPhaseTransitionController phaseController,
        ISpecificationDriftService driftService,
        ILogger<WorkflowOrchestrator> logger,
        IGitWorkflowService? gitWorkflowService = null,
        IAutoSaveService? autoSaveService = null,
        ISessionManager? sessionManager = null,
        ITokenTracker? tokenTracker = null,
        IFailureHandler? failureHandler = null,
        IBudgetEnforcer? budgetEnforcer = null,
        IPlanManager? planManager = null,
        IPauseController? pauseController = null,
        WorkflowOptions? workflowOptions = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _assessor = assessor ?? throw new ArgumentNullException(nameof(assessor));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
        _guardrailPipeline = guardrailPipeline ?? throw new ArgumentNullException(nameof(guardrailPipeline));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _phaseController = phaseController ?? throw new ArgumentNullException(nameof(phaseController));
        _driftService = driftService ?? throw new ArgumentNullException(nameof(driftService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitWorkflowService = gitWorkflowService;
        _autoSaveService = autoSaveService;
        _sessionManager = sessionManager;
        _tokenTracker = tokenTracker;
        _failureHandler = failureHandler;
        _budgetEnforcer = budgetEnforcer;
        _planManager = planManager;
        _pauseController = pauseController;
        _workflowOptions = workflowOptions;
    }

    public async Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        _userPrompt = userPrompt;
        _iterationCount = 0;

        // Ensure module-specific git branch before starting
        if (_gitWorkflowService is not null)
        {
            var branchResult = await _gitWorkflowService.EnsureModuleBranchAsync(moduleName, cancellationToken);
            if (branchResult is not null)
            {
                _logger.LogInformation("Git branch for module {Module}: {Success}",
                    moduleName, branchResult.Success);
            }
        }

        // Check for resumable session (STOR-07)
        if (_sessionManager is not null)
        {
            var latestId = await _sessionManager.GetLatestSessionIdAsync(cancellationToken);
            if (latestId is not null && latestId.Module.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                var savedState = await _sessionManager.LoadSessionStateAsync(latestId, cancellationToken);
                if (savedState is not null && !savedState.IsComplete)
                {
                    _logger.LogInformation("Resuming session {SessionId} for module {Module}",
                        latestId, moduleName);
                    _sessionId = latestId;
                    await _renderer.RenderResultAsync(
                        $"Resuming session {latestId} at step {savedState.Step}");
                }
            }

            // Create new session if not resuming
            _sessionId ??= await _sessionManager.CreateSessionAsync(moduleName, cancellationToken);
        }

        await _engine.InitializeAsync(moduleName, cancellationToken);
        _logger.LogInformation("Starting orchestration for module {Module} at step {Step}",
            moduleName, _engine.CurrentStep);

        while (!_engine.IsComplete && !cancellationToken.IsCancellationRequested)
        {
            // Wait if paused by user (TUI-41: Ctrl+P pause gate)
            if (_pauseController is not null && _pauseController.IsPaused)
            {
                _logger.LogInformation("Execution paused — waiting for resume");
                await _renderer.RenderResultAsync("Paused — press Ctrl+P to resume");
                await AutoSaveAsync(AutoSaveTrigger.UserPause, moduleName, cancellationToken);
                await _pauseController.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Execution resumed");
                await _renderer.RenderResultAsync("Resumed");
            }

            var stepResult = await RunStepAsync(moduleName, cancellationToken: cancellationToken);

            if (!stepResult.Success)
            {
                _logger.LogWarning("Step {Step} failed: {Summary}", _engine.CurrentStep, stepResult.Summary);
                await _renderer.RenderErrorAsync(stepResult.Summary ?? "Step failed");
                await AutoSaveAsync(AutoSaveTrigger.TaskFailure, moduleName, cancellationToken);

                // CORE-21: Consult failure handler for self-correction vs interruption
                if (_failureHandler is not null)
                {
                    var taskId = _engine.CurrentStep.ToString();

                    // CORE-23: Critical system errors bypass normal failure tracking and block immediately
                    if (stepResult.IsCriticalError)
                    {
                        var criticalClassification = _failureHandler.RecordCriticalError(
                            stepResult.Summary ?? "Critical system error");
                        _logger.LogCritical(
                            "Critical system error — blocking execution: {Message}",
                            criticalClassification.Message);
                        await _renderer.RenderErrorAsync(
                            $"CRITICAL ERROR — execution blocked: {criticalClassification.Message}");
                        await AutoSaveAsync(AutoSaveTrigger.TaskFailure, moduleName, cancellationToken);
                        return OrchestrationResult.CriticalError(_iterationCount, _engine.CurrentStep,
                            criticalClassification.Message);
                    }

                    var classification = _failureHandler.RecordFailure(taskId, stepResult.Summary ?? "Step failed");

                    if (classification.Action == FailureAction.SelfCorrect)
                    {
                        _logger.LogInformation(
                            "Self-correcting: {Message} (attempt {Count})",
                            classification.Message, classification.ConsecutiveFailures);
                        continue; // Let the LLM retry on next iteration
                    }

                    // CORE-22: Prompt user for intervention on repeated failures
                    if (classification.Action == FailureAction.PromptUser)
                    {
                        if (_workflowOptions?.Unattended == true)
                        {
                            _logger.LogWarning(
                                "Unattended mode — suppressing intervention prompt, continuing: {Message}",
                                classification.Message);
                            continue;
                        }

                        var promptMessage = $"Task '{taskId}' has failed {classification.ConsecutiveFailures} consecutive times. Continue? [y/N]";
                        var response = await _renderer.PromptAsync(promptMessage, cancellationToken);

                        if (response is not null &&
                            response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ||
                            response?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            _logger.LogInformation(
                                "User confirmed continuation after repeated failure: {TaskId}",
                                taskId);
                            _failureHandler.ResetFailureCount(taskId);
                            continue;
                        }

                        _logger.LogWarning(
                            "User declined continuation after repeated failure: {TaskId}",
                            taskId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failure escalated: {Action} — {Message}",
                            classification.Action, classification.Message);
                    }
                }

                return OrchestrationResult.Interrupted(_iterationCount, _engine.CurrentStep,
                    stepResult.Summary ?? "Step failed");
            }

            // Reset failure count on success when handler is present
            _failureHandler?.ResetFailureCount(_engine.CurrentStep.ToString());

            // Auto-save after each successful step (STOR-06)
            await AutoSaveAsync(AutoSaveTrigger.StepCompletion, moduleName, cancellationToken);

            if (stepResult.RequiresUserConfirmation)
            {
                _logger.LogInformation("User confirmation required at step {Step}", _engine.CurrentStep);
                await _renderer.RenderResultAsync(stepResult.Summary ?? "Awaiting user confirmation");
                await AutoSaveAsync(AutoSaveTrigger.UserPause, moduleName, cancellationToken);
                return OrchestrationResult.Interrupted(_iterationCount, _engine.CurrentStep,
                    "User confirmation required");
            }

            if (stepResult.NextTrigger.HasValue)
            {
                var previousPhase = _engine.CurrentPhase;
                var fired = _engine.Fire(stepResult.NextTrigger.Value);
                if (!fired)
                {
                    _logger.LogWarning("Cannot fire trigger {Trigger} from step {Step}",
                        stepResult.NextTrigger.Value, _engine.CurrentStep);
                }
                else if (_engine.CurrentPhase != previousPhase)
                {
                    // Phase transition occurred — save state
                    await AutoSaveAsync(AutoSaveTrigger.PhaseTransition, moduleName, cancellationToken);
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await AutoSaveAsync(AutoSaveTrigger.UserPause, moduleName, cancellationToken);
            return OrchestrationResult.Interrupted(_iterationCount, _engine.CurrentStep, "Cancelled");
        }

        _logger.LogInformation("Orchestration complete for module {Module} after {Iterations} iterations",
            moduleName, _iterationCount);
        await _renderer.RenderResultAsync($"Module {moduleName} completed after {_iterationCount} iterations");

        return OrchestrationResult.Completed(_iterationCount, _engine.CurrentStep);
    }

    public async Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        if (userPrompt is not null)
            _userPrompt = userPrompt;
        _iterationCount++;

        var currentStep = _engine.CurrentStep;
        var currentPhase = _engine.CurrentPhase;

        // OTEL-02: Workflow phase span
        using var phaseActivity = SpanFactory.StartWorkflowPhase(
            currentPhase.ToString(), moduleName, _iterationCount);

        _logger.LogDebug("Iteration {Iteration}: step={Step}, phase={Phase}",
            _iterationCount, currentStep, currentPhase);

        // OTEL gauge: current iteration
        LopenTelemetryDiagnostics.SessionIteration.Record(_iterationCount);

        // 0. Check budget before LLM invocation (CFG-12)
        var budgetStepResult = await CheckBudgetAsync(moduleName);
        if (budgetStepResult is not null)
            return budgetStepResult;

        // 1. Evaluate guardrails before LLM invocation
        var guardrailContext = new GuardrailContext(moduleName, null, _iterationCount, 0);
        var guardrailResults = await _guardrailPipeline.EvaluateAsync(guardrailContext, cancellationToken);

        foreach (var result in guardrailResults)
        {
            if (result is GuardrailResult.Block block)
            {
                // OTEL-07: Back-pressure event span + counter
                using var bpActivity = SpanFactory.StartBackpressure("guardrail", "block", block.Message);
                LopenTelemetryDiagnostics.BackPressureEventCount.Add(1,
                    new KeyValuePair<string, object?>("lopen.backpressure.action", "block"));

                _logger.LogWarning("Guardrail blocked: {Message}", block.Message);
                return StepResult.Failed($"Blocked by guardrail: {block.Message}");
            }

            if (result is GuardrailResult.Warn warn)
            {
                using var warnActivity = SpanFactory.StartBackpressure("guardrail", "warn", warn.Message);
                LopenTelemetryDiagnostics.BackPressureEventCount.Add(1,
                    new KeyValuePair<string, object?>("lopen.backpressure.action", "warn"));

                _logger.LogWarning("Guardrail warning: {Message}", warn.Message);
                await _renderer.RenderErrorAsync($"Warning: {warn.Message}");
            }
        }

        // 2. Assess specification drift at re-entry
        var driftResults = await _driftService.CheckDriftAsync(moduleName, cancellationToken);
        if (driftResults.Count > 0)
        {
            foreach (var drift in driftResults)
            {
                var action = drift.IsNew ? "added" : drift.IsRemoved ? "removed" : "changed";
                _logger.LogWarning("Spec drift: section '{Header}' was {Action}", drift.Header, action);
                await _renderer.RenderErrorAsync(
                    $"Specification drift detected: section '{drift.Header}' was {action}");
            }
        }

        // 3. Check phase-specific transition rules
        if (currentStep == WorkflowStep.DraftSpecification)
        {
            if (!_phaseController.IsRequirementGatheringToPlannningApproved)
            {
                // Invoke LLM for spec drafting, then pause for human gate
                var specResult = await InvokeLlmForStepAsync(moduleName, currentStep, currentPhase, cancellationToken);
                if (!specResult.Success) return specResult;
                return StepResult.NeedsConfirmation(
                    specResult.Summary ?? "Specification drafted. Please review and approve to continue.");
            }

            // Spec already approved, fire transition
            return StepResult.Succeeded(WorkflowTrigger.SpecApproved, "Specification approved");
        }

        // 4. Invoke LLM for current step
        // OTEL-06: Task execution span when iterating through tasks
        Activity? taskActivity = null;
        var taskStopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (currentStep == WorkflowStep.IterateThroughTasks)
        {
            taskActivity = SpanFactory.StartTask(
                $"iteration-{_iterationCount}", "current", moduleName);
        }

        var llmResult = await InvokeLlmForStepAsync(moduleName, currentStep, currentPhase, cancellationToken);
        taskStopwatch.Stop();

        if (taskActivity is not null)
        {
            SpanFactory.SetTaskResult(taskActivity,
                llmResult.Success ? "success" : "failed", _iterationCount);
            taskActivity.Dispose();

            // OTEL task counters + duration
            if (llmResult.Success)
                LopenTelemetryDiagnostics.TasksCompletedCount.Add(1);
            else
                LopenTelemetryDiagnostics.TasksFailedCount.Add(1);
            LopenTelemetryDiagnostics.TaskDuration.Record(
                taskStopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("lopen.task.module", moduleName));
        }

        if (!llmResult.Success) return llmResult;

        // 5. Check auto-transition conditions
        if (currentStep == WorkflowStep.BreakIntoTasks)
        {
            // STOR-09: Persist plan content after task breakdown
            await PersistPlanAsync(moduleName, llmResult.Summary, cancellationToken);

            // Check if planning is structurally complete for auto-transition to building
            var hasComponents = await _assessor.HasMoreComponentsAsync(moduleName, cancellationToken);
            if (_phaseController.CanAutoTransitionToBuilding(true, true))
            {
                _logger.LogInformation("Auto-transition: Planning structurally complete");
            }
        }

        if (currentStep == WorkflowStep.Repeat)
        {
            // Check if all components are built for auto-transition to complete
            var hasMore = await _assessor.HasMoreComponentsAsync(moduleName, cancellationToken);
            if (!hasMore)
            {
                _logger.LogInformation("No more components — checking module completion");
                return StepResult.Succeeded(WorkflowTrigger.ModuleComplete, "All components complete");
            }
        }

        // 6. Determine next trigger based on current step
        var nextTrigger = DetermineNextTrigger(currentStep);
        if (nextTrigger.HasValue)
        {
            return StepResult.Succeeded(nextTrigger.Value, llmResult.Summary);
        }

        return llmResult;
    }

    private async Task<StepResult> InvokeLlmForStepAsync(
        string moduleName, WorkflowStep step, WorkflowPhase phase,
        CancellationToken cancellationToken)
    {
        await _renderer.RenderProgressAsync(
            phase.ToString(), step.ToString(), CalculateProgress(step), cancellationToken);

        var model = _modelSelector.SelectModel(phase);
        var tools = _toolRegistry.GetToolsForPhase(phase);
        var contextSections = _userPrompt is not null
            ? new Dictionary<string, string> { ["user_prompt"] = _userPrompt }
            : null;
        var systemPrompt = _promptBuilder.BuildSystemPrompt(phase, moduleName, null, null, contextSections);

        // OTEL-03: SDK invocation span
        using var sdkActivity = SpanFactory.StartSdkInvocation(model.SelectedModel);
        var sdkStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await _llmService.InvokeAsync(systemPrompt, model.SelectedModel, tools, cancellationToken);

            sdkStopwatch.Stop();
            _logger.LogInformation(
                "LLM invocation complete: {Tokens} tokens, {ToolCalls} tool calls",
                result.TokenUsage.TotalTokens, result.ToolCallsMade);

            // Record token usage for session metrics (LLM-13)
            _tokenTracker?.RecordUsage(result.TokenUsage);

            SpanFactory.SetSdkResult(sdkActivity,
                result.TokenUsage.InputTokens, result.TokenUsage.OutputTokens,
                result.TokenUsage.IsPremiumRequest, result.ToolCallsMade);

            // OTEL counters and histograms
            LopenTelemetryDiagnostics.SdkInvocationCount.Add(1,
                new KeyValuePair<string, object?>("lopen.sdk.model", model.SelectedModel));
            LopenTelemetryDiagnostics.TokensConsumed.Add(result.TokenUsage.TotalTokens,
                new KeyValuePair<string, object?>("lopen.sdk.model", model.SelectedModel));
            LopenTelemetryDiagnostics.SdkInvocationDuration.Record(
                sdkStopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("lopen.sdk.model", model.SelectedModel));
            if (result.TokenUsage.IsPremiumRequest)
            {
                LopenTelemetryDiagnostics.PremiumRequestCount.Add(1);
            }
            if (result.TokenUsage.ContextWindowSize > 0)
            {
                LopenTelemetryDiagnostics.ContextWindowUtilization.Record(
                    (double)result.TokenUsage.TotalTokens / result.TokenUsage.ContextWindowSize);
            }

            return StepResult.Succeeded(
                DetermineNextTrigger(step) ?? WorkflowTrigger.Assess,
                result.Output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsCriticalException(ex))
        {
            // CORE-23: Critical system errors block execution
            sdkActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogCritical(ex, "Critical system error at step {Step}", step);
            await _renderer.RenderErrorAsync($"CRITICAL: {ex.Message}", ex);
            return StepResult.CriticalFailure($"Critical system error: {ex.Message}");
        }
        catch (Exception ex)
        {
            sdkActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "LLM invocation failed at step {Step}", step);
            await _renderer.RenderErrorAsync($"LLM error: {ex.Message}", ex);
            return StepResult.Failed($"LLM invocation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks budget limits before each LLM invocation (CFG-12).
    /// Returns null to proceed, or a StepResult to short-circuit the step.
    /// </summary>
    private async Task<StepResult?> CheckBudgetAsync(string moduleName)
    {
        if (_budgetEnforcer is null || _tokenTracker is null)
            return null;

        var metrics = _tokenTracker.GetSessionMetrics();
        long totalTokens = metrics.CumulativeInputTokens + metrics.CumulativeOutputTokens;
        var check = _budgetEnforcer.Check(totalTokens, metrics.PremiumRequestCount);

        switch (check.Status)
        {
            case BudgetStatus.Ok:
                return null;

            case BudgetStatus.Warning:
                _logger.LogWarning("Budget warning: {Message}", check.Message);
                await _renderer.RenderErrorAsync($"Warning: {check.Message}");
                return null; // Continue

            case BudgetStatus.ConfirmationRequired:
                if (_workflowOptions?.Unattended == true)
                {
                    _logger.LogWarning(
                        "Budget confirmation required in unattended mode — halting: {Message}",
                        check.Message);
                    await AutoSaveAsync(AutoSaveTrigger.UserPause, moduleName, default);
                    return StepResult.Failed(
                        $"Budget confirmation required (unattended): {check.Message}");
                }

                _logger.LogWarning("Budget confirmation required: {Message}", check.Message);
                var response = await _renderer.PromptAsync(
                    $"{check.Message} Continue? [y/N]", default);

                if (response is not null &&
                    (response.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ||
                     response.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("User confirmed budget continuation");
                    return null; // Continue
                }

                _logger.LogWarning("User declined budget continuation");
                await AutoSaveAsync(AutoSaveTrigger.UserPause, moduleName, default);
                return StepResult.Failed($"Budget confirmation declined: {check.Message}");

            case BudgetStatus.Exceeded:
                _logger.LogWarning("Budget exceeded — halting: {Message}", check.Message);
                await AutoSaveAsync(AutoSaveTrigger.UserPause, moduleName, default);
                return StepResult.Failed($"Budget exceeded: {check.Message}");

            default:
                return null;
        }
    }

    private static WorkflowTrigger? DetermineNextTrigger(WorkflowStep step) => step switch
    {
        WorkflowStep.DraftSpecification => WorkflowTrigger.SpecApproved,
        WorkflowStep.DetermineDependencies => WorkflowTrigger.DependenciesDetermined,
        WorkflowStep.IdentifyComponents => WorkflowTrigger.ComponentsIdentified,
        WorkflowStep.SelectNextComponent => WorkflowTrigger.ComponentSelected,
        WorkflowStep.BreakIntoTasks => WorkflowTrigger.TasksBrokenDown,
        WorkflowStep.IterateThroughTasks => WorkflowTrigger.TaskIterationComplete,
        WorkflowStep.Repeat => WorkflowTrigger.Assess,
        _ => null
    };

    private static double CalculateProgress(WorkflowStep step) => step switch
    {
        WorkflowStep.DraftSpecification => 0.05,
        WorkflowStep.DetermineDependencies => 0.15,
        WorkflowStep.IdentifyComponents => 0.25,
        WorkflowStep.SelectNextComponent => 0.35,
        WorkflowStep.BreakIntoTasks => 0.45,
        WorkflowStep.IterateThroughTasks => 0.70,
        WorkflowStep.Repeat => 0.90,
        _ => 0.0
    };

    private async Task AutoSaveAsync(AutoSaveTrigger trigger, string moduleName, CancellationToken cancellationToken)
    {
        if (_autoSaveService is null || _sessionId is null) return;

        var state = new SessionState
        {
            SessionId = _sessionId.ToString(),
            Module = moduleName,
            Phase = _engine.CurrentPhase.ToString(),
            Step = _engine.CurrentStep.ToString(),
            IsComplete = _engine.IsComplete,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Build session metrics from token tracker if available
        SessionMetrics? metrics = null;
        if (_tokenTracker is not null)
        {
            var tokenMetrics = _tokenTracker.GetSessionMetrics();
            metrics = new SessionMetrics
            {
                SessionId = _sessionId.ToString(),
                CumulativeInputTokens = tokenMetrics.CumulativeInputTokens,
                CumulativeOutputTokens = tokenMetrics.CumulativeOutputTokens,
                PremiumRequestCount = tokenMetrics.PremiumRequestCount,
                IterationCount = _iterationCount,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
        }

        try
        {
            await _autoSaveService.SaveAsync(trigger, _sessionId, state, metrics, cancellationToken);
            _logger.LogDebug("Auto-saved state: trigger={Trigger}, step={Step}", trigger, _engine.CurrentStep);
        }
        catch (Exception ex)
        {
            // Auto-save failures must not crash the workflow (STOR-06)
            _logger.LogWarning(ex, "Auto-save failed for trigger {Trigger}", trigger);
        }
    }

    /// <summary>
    /// Persists plan content after BreakIntoTasks succeeds (STOR-09).
    /// Appends new component tasks to any existing plan content.
    /// Failures are logged but do not block the workflow.
    /// </summary>
    private async Task PersistPlanAsync(string moduleName, string? planContent, CancellationToken cancellationToken)
    {
        if (_planManager is null || string.IsNullOrWhiteSpace(planContent))
            return;

        try
        {
            var existingContent = await _planManager.ReadPlanAsync(moduleName, cancellationToken);
            var finalContent = string.IsNullOrWhiteSpace(existingContent)
                ? planContent
                : existingContent + "\n\n" + planContent;

            await _planManager.WritePlanAsync(moduleName, finalContent, cancellationToken);
            _logger.LogInformation("Plan persisted for module {Module}", moduleName);
        }
        catch (Exception ex)
        {
            // Plan persistence failures must not crash the workflow
            _logger.LogWarning(ex, "Failed to persist plan for module {Module}", moduleName);
        }
    }

    /// <summary>
    /// Determines whether an exception represents a critical system error
    /// that should block workflow execution (CORE-23).
    /// Critical: I/O failures, permission errors, out-of-memory, security exceptions.
    /// </summary>
    private static bool IsCriticalException(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or OutOfMemoryException
            or System.Security.SecurityException;
}
