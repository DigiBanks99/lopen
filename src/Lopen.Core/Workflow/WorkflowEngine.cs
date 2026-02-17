using Lopen.Llm;
using Microsoft.Extensions.Logging;
using Stateless;

namespace Lopen.Core.Workflow;

/// <summary>
/// Stateless-based workflow engine implementing the 7-step development loop.
/// Transitions: DraftSpec → Dependencies → Components → Select → Tasks → Iterate → Repeat.
/// </summary>
internal sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly StateMachine<WorkflowStep, WorkflowTrigger> _machine;
    private readonly IStateAssessor _assessor;
    private readonly ILogger<WorkflowEngine> _logger;
    private WorkflowStep _currentStep = WorkflowStep.DraftSpecification;
    private bool _isComplete;

    public WorkflowEngine(IStateAssessor assessor, ILogger<WorkflowEngine> logger)
    {
        _assessor = assessor ?? throw new ArgumentNullException(nameof(assessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _machine = new StateMachine<WorkflowStep, WorkflowTrigger>(
            () => _currentStep,
            s => _currentStep = s);

        ConfigureStateMachine();
    }

    public WorkflowStep CurrentStep => _currentStep;

    public bool IsComplete => _isComplete;

    public WorkflowPhase CurrentPhase => MapStepToPhase(CurrentStep);

    public async Task InitializeAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        _currentStep = await _assessor.GetCurrentStepAsync(moduleName, cancellationToken);
        _isComplete = false;

        _logger.LogInformation(
            "Workflow initialized for module {Module} at step {Step} (phase: {Phase})",
            moduleName, CurrentStep, CurrentPhase);
    }

    public bool Fire(WorkflowTrigger trigger)
    {
        if (!_machine.CanFire(trigger))
        {
            _logger.LogWarning(
                "Cannot fire trigger {Trigger} from step {Step}",
                trigger, CurrentStep);
            return false;
        }

        var previousStep = CurrentStep;
        _machine.Fire(trigger);

        _logger.LogInformation(
            "Workflow transitioned from {Previous} to {Current} via {Trigger}",
            previousStep, CurrentStep, trigger);

        return true;
    }

    public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers()
    {
#pragma warning disable CS0618 // Synchronous API is fine for this non-async context
        return _machine.PermittedTriggers.ToList().AsReadOnly();
#pragma warning restore CS0618
    }

    private void ConfigureStateMachine()
    {
        // Step 1: Draft Specification (Requirement Gathering phase)
        _machine.Configure(WorkflowStep.DraftSpecification)
            .Permit(WorkflowTrigger.SpecApproved, WorkflowStep.DetermineDependencies);

        // Step 2: Determine Dependencies (Planning phase)
        _machine.Configure(WorkflowStep.DetermineDependencies)
            .Permit(WorkflowTrigger.DependenciesDetermined, WorkflowStep.IdentifyComponents);

        // Step 3: Identify Components (Planning phase)
        _machine.Configure(WorkflowStep.IdentifyComponents)
            .Permit(WorkflowTrigger.ComponentsIdentified, WorkflowStep.SelectNextComponent);

        // Step 4: Select Next Component (Planning phase)
        _machine.Configure(WorkflowStep.SelectNextComponent)
            .Permit(WorkflowTrigger.ComponentSelected, WorkflowStep.BreakIntoTasks)
            .Permit(WorkflowTrigger.ModuleComplete, WorkflowStep.Repeat);

        // Step 5: Break Into Tasks (Planning phase)
        _machine.Configure(WorkflowStep.BreakIntoTasks)
            .Permit(WorkflowTrigger.TasksBrokenDown, WorkflowStep.IterateThroughTasks);

        // Step 6: Iterate Through Tasks (Building phase)
        _machine.Configure(WorkflowStep.IterateThroughTasks)
            .Permit(WorkflowTrigger.ComponentComplete, WorkflowStep.Repeat)
            .PermitReentry(WorkflowTrigger.TaskIterationComplete);

        // Step 7: Repeat — loops back to SelectNextComponent or marks complete
        _machine.Configure(WorkflowStep.Repeat)
            .Permit(WorkflowTrigger.Assess, WorkflowStep.SelectNextComponent)
            .OnEntry(transition =>
            {
                if (transition.Trigger == WorkflowTrigger.ModuleComplete)
                {
                    _isComplete = true;
                    _logger.LogInformation("Workflow complete — all components done");
                }
            });
    }

    internal static WorkflowPhase MapStepToPhase(WorkflowStep step) => step switch
    {
        WorkflowStep.DraftSpecification => WorkflowPhase.RequirementGathering,
        WorkflowStep.DetermineDependencies => WorkflowPhase.Planning,
        WorkflowStep.IdentifyComponents => WorkflowPhase.Planning,
        WorkflowStep.SelectNextComponent => WorkflowPhase.Planning,
        WorkflowStep.BreakIntoTasks => WorkflowPhase.Planning,
        WorkflowStep.IterateThroughTasks => WorkflowPhase.Building,
        WorkflowStep.Repeat => WorkflowPhase.Building,
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown workflow step")
    };
}
