using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lopen.Core.Tests.Workflow;

/// <summary>
/// Integration tests verifying WorkflowOrchestrator + RetryingLlmService + GuardrailPipeline
/// work together end-to-end (CORE-02, JOB-016).
/// </summary>
public sealed class OrchestratorRetryGuardrailIntegrationTests
{
    private readonly FakeInnerLlmService _innerLlm = new();
    private readonly FakeModelSelector _modelSelector = new();
    private readonly FakePromptBuilder _promptBuilder = new();
    private readonly FakeToolRegistry _toolRegistry = new();
    private readonly FakeOutputRenderer _renderer = new();
    private readonly FakePhaseTransitionController _phaseController = new();
    private readonly FakeSpecificationDriftService _driftService = new();

    private RetryingLlmService CreateRetryingLlmService() =>
        new(
            _innerLlm,
            _modelSelector,
            Options.Create(new LopenOptions()),
            NullLogger<RetryingLlmService>.Instance);

    private WorkflowOrchestrator CreateOrchestrator(
        ILlmService llmService,
        IGuardrailPipeline guardrailPipeline,
        FakeWorkflowEngine engine,
        FakeStateAssessor assessor) =>
        new(
            engine,
            assessor,
            llmService,
            _promptBuilder,
            _toolRegistry,
            _modelSelector,
            guardrailPipeline,
            _renderer,
            _phaseController,
            _driftService,
            NullLogger<WorkflowOrchestrator>.Instance,
            workflowOptions: new WorkflowOptions { FailureThreshold = 50 });

    [Fact]
    public async Task FullPipeline_GuardrailsPass_RetryingLlmInvokesInner_OrchestratorCompletes()
    {
        // Arrange: start at DetermineDependencies (avoids spec-approval gate), LLM succeeds
        var engine = new FakeWorkflowEngine(WorkflowStep.DetermineDependencies, completeAfterFires: 1);
        var assessor = new FakeStateAssessor();
        var guardrails = new GuardrailPipeline(new IGuardrail[] { new PassGuardrail() });
        var retryingLlm = CreateRetryingLlmService();
        var orchestrator = CreateOrchestrator(retryingLlm, guardrails, engine, assessor);

        // Act
        var result = await orchestrator.RunAsync("test-module");

        // Assert: orchestrator completed and inner LLM was called through RetryingLlmService
        Assert.True(result.IsComplete);
        Assert.True(_innerLlm.InvokeCount > 0, "RetryingLlmService should have called the inner ILlmService");
    }

    [Fact]
    public async Task FullPipeline_GuardrailBlocks_LlmNotInvoked_StepFails()
    {
        // Arrange: guardrail blocks immediately, LLM should never be called
        var engine = new FakeWorkflowEngine(WorkflowStep.DetermineDependencies, completeAfterFires: 1);
        var assessor = new FakeStateAssessor();
        var guardrails = new GuardrailPipeline(new IGuardrail[] { new BlockingGuardrail("Resource limit exceeded") });
        var retryingLlm = CreateRetryingLlmService();
        var orchestrator = CreateOrchestrator(retryingLlm, guardrails, engine, assessor);

        // Act
        var result = await orchestrator.RunAsync("test-module");

        // Assert: orchestrator was interrupted, inner LLM was never called
        Assert.True(result.WasInterrupted);
        Assert.Equal(0, _innerLlm.InvokeCount);
    }

    [Fact]
    public async Task FullPipeline_ModelUnavailable_RetryingLlmFallsBack_OrchestratorCompletes()
    {
        // Arrange: primary model unavailable, fallback succeeds
        _innerLlm.FailForModels.Add("gpt-4");
        _modelSelector.FallbackChains[WorkflowPhase.Planning] = new[] { "gpt-4", "gpt-3.5-turbo" };

        var engine = new FakeWorkflowEngine(WorkflowStep.DetermineDependencies, completeAfterFires: 1);
        var assessor = new FakeStateAssessor();
        var guardrails = new GuardrailPipeline(new IGuardrail[] { new PassGuardrail() });
        var retryingLlm = CreateRetryingLlmService();
        var orchestrator = CreateOrchestrator(retryingLlm, guardrails, engine, assessor);

        // Act
        var result = await orchestrator.RunAsync("test-module");

        // Assert: completed via fallback model
        Assert.True(result.IsComplete);
        Assert.True(_innerLlm.InvokeCount >= 2, "Should have tried primary then fallback model");
        Assert.Contains("gpt-3.5-turbo", _innerLlm.ModelsInvoked);
    }

    [Fact]
    public async Task FullPipeline_GuardrailWarns_LlmStillInvoked_OrchestratorCompletes()
    {
        // Arrange: guardrail warns but doesn't block
        var engine = new FakeWorkflowEngine(WorkflowStep.DetermineDependencies, completeAfterFires: 1);
        var assessor = new FakeStateAssessor();
        var guardrails = new GuardrailPipeline(new IGuardrail[]
        {
            new WarningGuardrail("High iteration count detected"),
            new PassGuardrail()
        });
        var retryingLlm = CreateRetryingLlmService();
        var orchestrator = CreateOrchestrator(retryingLlm, guardrails, engine, assessor);

        // Act
        var result = await orchestrator.RunAsync("test-module");

        // Assert: completed despite warning, LLM was called
        Assert.True(result.IsComplete);
        Assert.True(_innerLlm.InvokeCount > 0);
    }

    [Fact]
    public async Task FullPipeline_AllModelsFail_OrchestratorHandlesFailure()
    {
        // Arrange: all fallback models fail with model-unavailable
        _innerLlm.FailForModels.Add("gpt-4");
        _innerLlm.FailForModels.Add("gpt-3.5-turbo");
        _modelSelector.FallbackChains[WorkflowPhase.Planning] = new[] { "gpt-4", "gpt-3.5-turbo" };

        var engine = new FakeWorkflowEngine(WorkflowStep.DetermineDependencies, completeAfterFires: 1);
        var assessor = new FakeStateAssessor();
        var guardrails = new GuardrailPipeline(new IGuardrail[] { new PassGuardrail() });
        var retryingLlm = CreateRetryingLlmService();
        var orchestrator = CreateOrchestrator(retryingLlm, guardrails, engine, assessor);

        // Act
        var result = await orchestrator.RunAsync("test-module");

        // Assert: LLM failure causes step failure — orchestrator handles via self-correct loop
        // (with high FailureThreshold=50 it will retry, but all models fail each time)
        Assert.True(_innerLlm.InvokeCount > 0, "RetryingLlmService should have attempted model calls");
        Assert.True(_innerLlm.ModelsInvoked.Contains("gpt-4"), "Should have tried primary model");
    }

    #region Test Doubles

    private sealed class FakeInnerLlmService : ILlmService
    {
        public int InvokeCount;
        public List<string> ModelsInvoked { get; } = new();
        public HashSet<string> FailForModels { get; } = new();

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt, string model,
            IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            InvokeCount++;
            ModelsInvoked.Add(model);

            if (FailForModels.Contains(model))
            {
                throw new LlmException($"Model {model} is unavailable", model)
                {
                    IsModelUnavailable = true
                };
            }

            return Task.FromResult(new LlmInvocationResult(
                "output", new TokenUsage(10, 20, 30, 4096, false), 0, true));
        }
    }

    private sealed class FakeModelSelector : IModelSelector
    {
        public Dictionary<WorkflowPhase, string[]> FallbackChains { get; } = new();

        public ModelFallbackResult SelectModel(WorkflowPhase phase) =>
            new("gpt-4", false);

        public IReadOnlyList<string> GetFallbackChain(WorkflowPhase phase) =>
            FallbackChains.TryGetValue(phase, out var chain) ? chain : new[] { "gpt-4" };
    }

    private sealed class FakeWorkflowEngine : IWorkflowEngine
    {
        private int _fireCount;
        private readonly int _completeAfterFires;
        private bool _isComplete;

        public FakeWorkflowEngine(WorkflowStep initialStep, int completeAfterFires)
        {
            CurrentStep = initialStep;
            _completeAfterFires = completeAfterFires;
        }

        public WorkflowStep CurrentStep { get; private set; }
        public bool IsComplete => _isComplete;

        public WorkflowPhase CurrentPhase => CurrentStep switch
        {
            WorkflowStep.DraftSpecification => WorkflowPhase.RequirementGathering,
            WorkflowStep.IterateThroughTasks => WorkflowPhase.Building,
            _ => WorkflowPhase.Planning,
        };

        public Task InitializeAsync(string moduleName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public bool Fire(WorkflowTrigger trigger)
        {
            _fireCount++;
            if (_fireCount >= _completeAfterFires)
            {
                CurrentStep = WorkflowStep.Repeat;
                _isComplete = true;
            }
            return true;
        }

        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() =>
            Array.Empty<WorkflowTrigger>();
    }

    private sealed class FakeStateAssessor : IStateAssessor
    {
        public Task<WorkflowStep> GetCurrentStepAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(WorkflowStep.DetermineDependencies);

        public Task PersistStepAsync(string moduleName, WorkflowStep step, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> IsSpecReadyAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> HasMoreComponentsAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class FakePromptBuilder : IPromptBuilder
    {
        public string BuildSystemPrompt(WorkflowPhase phase, string module, string? component, string? task,
            IReadOnlyDictionary<string, string>? contextSections = null) =>
            $"System prompt for {phase}";
    }

    private sealed class FakeToolRegistry : IToolRegistry
    {
        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase) =>
            Array.Empty<LopenToolDefinition>();

        public void RegisterTool(LopenToolDefinition tool) { }
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => Array.Empty<LopenToolDefinition>();
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler) => false;
    }

    private sealed class FakeOutputRenderer : IOutputRenderer
    {
        public List<string> RenderedMessages { get; } = new();

        public Task RenderProgressAsync(string phase, string step, double progress, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RenderErrorAsync(string message, Exception? exception = null, CancellationToken ct = default)
        {
            RenderedMessages.Add($"ERROR: {message}");
            return Task.CompletedTask;
        }

        public Task RenderResultAsync(string message, CancellationToken ct = default)
        {
            RenderedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<string?> PromptAsync(string message, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakePhaseTransitionController : IPhaseTransitionController
    {
        public bool IsRequirementGatheringToPlannningApproved => true;
        public void ApproveSpecification() { }
        public void ResetApproval() { }
        public bool CanAutoTransitionToBuilding(bool hasComponentsIdentified, bool hasTasksBreakdown) =>
            hasComponentsIdentified && hasTasksBreakdown;
        public bool CanAutoTransitionToComplete(bool allComponentsBuilt, bool allAcceptanceCriteriaPassed) =>
            allComponentsBuilt && allAcceptanceCriteriaPassed;
    }

    private sealed class FakeSpecificationDriftService : ISpecificationDriftService
    {
        public Task<IReadOnlyList<DriftResult>> CheckDriftAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DriftResult>>(Array.Empty<DriftResult>());
    }

    private sealed class PassGuardrail : IGuardrail
    {
        public int Order => 0;
        public bool ShortCircuitOnBlock => false;

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default) =>
            Task.FromResult<GuardrailResult>(new GuardrailResult.Pass());
    }

    private sealed class BlockingGuardrail : IGuardrail
    {
        private readonly string _message;
        public BlockingGuardrail(string message) => _message = message;
        public int Order => 0;
        public bool ShortCircuitOnBlock => true;

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default) =>
            Task.FromResult<GuardrailResult>(new GuardrailResult.Block(_message));
    }

    private sealed class WarningGuardrail : IGuardrail
    {
        private readonly string _message;
        public WarningGuardrail(string message) => _message = message;
        public int Order => 0;
        public bool ShortCircuitOnBlock => false;

        public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken ct = default) =>
            Task.FromResult<GuardrailResult>(new GuardrailResult.Warn(_message));
    }

    #endregion
}
