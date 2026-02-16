using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Llm;
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
    private readonly ILogger<WorkflowOrchestrator> _logger;

    private int _iterationCount;
    private SessionId? _sessionId;

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
        ITokenTracker? tokenTracker = null)
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
    }

    public async Task<OrchestrationResult> RunAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

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
            var stepResult = await RunStepAsync(moduleName, cancellationToken);

            if (!stepResult.Success)
            {
                _logger.LogWarning("Step {Step} failed: {Summary}", _engine.CurrentStep, stepResult.Summary);
                await _renderer.RenderErrorAsync(stepResult.Summary ?? "Step failed");
                await AutoSaveAsync(AutoSaveTrigger.TaskFailure, moduleName, cancellationToken);
                return OrchestrationResult.Interrupted(_iterationCount, _engine.CurrentStep,
                    stepResult.Summary ?? "Step failed");
            }

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

    public async Task<StepResult> RunStepAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        _iterationCount++;

        var currentStep = _engine.CurrentStep;
        var currentPhase = _engine.CurrentPhase;
        _logger.LogDebug("Iteration {Iteration}: step={Step}, phase={Phase}",
            _iterationCount, currentStep, currentPhase);

        // 1. Evaluate guardrails before LLM invocation
        var guardrailContext = new GuardrailContext(moduleName, null, _iterationCount, 0);
        var guardrailResults = await _guardrailPipeline.EvaluateAsync(guardrailContext, cancellationToken);

        foreach (var result in guardrailResults)
        {
            if (result is GuardrailResult.Block block)
            {
                _logger.LogWarning("Guardrail blocked: {Message}", block.Message);
                return StepResult.Failed($"Blocked by guardrail: {block.Message}");
            }

            if (result is GuardrailResult.Warn warn)
            {
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
        var llmResult = await InvokeLlmForStepAsync(moduleName, currentStep, currentPhase, cancellationToken);
        if (!llmResult.Success) return llmResult;

        // 5. Check auto-transition conditions
        if (currentStep == WorkflowStep.BreakIntoTasks)
        {
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
        var systemPrompt = _promptBuilder.BuildSystemPrompt(phase, moduleName, null, null);

        try
        {
            var result = await _llmService.InvokeAsync(systemPrompt, model.SelectedModel, tools, cancellationToken);

            _logger.LogInformation(
                "LLM invocation complete: {Tokens} tokens, {ToolCalls} tool calls",
                result.TokenUsage.TotalTokens, result.ToolCallsMade);

            // Record token usage for session metrics (LLM-13)
            _tokenTracker?.RecordUsage(result.TokenUsage);

            return StepResult.Succeeded(
                DetermineNextTrigger(step) ?? WorkflowTrigger.Assess,
                result.Output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM invocation failed at step {Step}", step);
            await _renderer.RenderErrorAsync($"LLM error: {ex.Message}", ex);
            return StepResult.Failed($"LLM invocation failed: {ex.Message}");
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
}
