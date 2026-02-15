using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Implements phase transition rules:
/// - Req Gathering → Planning: human-gated (requires ApproveSpecification)
/// - Planning → Building: auto when plan structurally complete
/// - Building → Complete: auto when all components built + all ACs pass
/// </summary>
internal sealed class PhaseTransitionController : IPhaseTransitionController
{
    private readonly ILogger<PhaseTransitionController> _logger;
    private bool _specApproved;

    public PhaseTransitionController(ILogger<PhaseTransitionController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsRequirementGatheringToPlannningApproved => _specApproved;

    public void ApproveSpecification()
    {
        _specApproved = true;
        _logger.LogInformation("Specification approved — human gate passed");
    }

    public void ResetApproval()
    {
        _specApproved = false;
        _logger.LogDebug("Specification approval reset");
    }

    public bool CanAutoTransitionToBuilding(bool hasComponentsIdentified, bool hasTasksBreakdown)
    {
        var canTransition = hasComponentsIdentified && hasTasksBreakdown;

        if (canTransition)
        {
            _logger.LogInformation("Auto-transition: Planning → Building (plan structurally complete)");
        }

        return canTransition;
    }

    public bool CanAutoTransitionToComplete(bool allComponentsBuilt, bool allAcceptanceCriteriaPassed)
    {
        var canTransition = allComponentsBuilt && allAcceptanceCriteriaPassed;

        if (canTransition)
        {
            _logger.LogInformation("Auto-transition: Building → Complete (all components + ACs pass)");
        }

        return canTransition;
    }
}
