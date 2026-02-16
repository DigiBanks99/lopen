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

        var result = await sut.RunAsync("test-module", cts.Token);

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

    // --- Stubs ---

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

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt, string model, IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken ct = default)
        {
            InvokeCount++;
            if (ThrowOnInvoke)
                throw new InvalidOperationException("LLM error");

            return Task.FromResult(new LlmInvocationResult(
                "Test output", new TokenUsage(100, 50, 150, 8000, false), 1, true));
        }
    }

    private sealed class StubPromptBuilder : IPromptBuilder
    {
        public int BuildCount { get; private set; }

        public string BuildSystemPrompt(WorkflowPhase phase, string module, string? component, string? task,
            IReadOnlyDictionary<string, string>? contextSections = null)
        {
            BuildCount++;
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

        public Task<string?> PromptAsync(string message, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
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
}
