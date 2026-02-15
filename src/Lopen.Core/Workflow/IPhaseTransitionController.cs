namespace Lopen.Core.Workflow;

/// <summary>
/// Controls phase transitions in the workflow, implementing both
/// human-gated and automatic transitions per the specification.
/// </summary>
public interface IPhaseTransitionController
{
    /// <summary>
    /// Checks if the transition from Requirement Gathering to Planning is allowed.
    /// This is a human-gated transition requiring explicit user confirmation.
    /// </summary>
    bool IsRequirementGatheringToPlannningApproved { get; }

    /// <summary>
    /// Records user approval for the Requirement Gathering → Planning transition.
    /// </summary>
    void ApproveSpecification();

    /// <summary>
    /// Resets the approval state (e.g., when returning to requirement gathering).
    /// </summary>
    void ResetApproval();

    /// <summary>
    /// Checks if the Planning → Building auto-transition condition is met.
    /// Planning is complete when all components are identified and tasks are broken down.
    /// </summary>
    /// <param name="hasComponentsIdentified">Whether components have been identified.</param>
    /// <param name="hasTasksBreakdown">Whether tasks have been broken down.</param>
    /// <returns>True if planning is structurally complete.</returns>
    bool CanAutoTransitionToBuilding(bool hasComponentsIdentified, bool hasTasksBreakdown);

    /// <summary>
    /// Checks if the Building → Complete auto-transition condition is met.
    /// Building is complete when all components are built and all ACs pass.
    /// </summary>
    /// <param name="allComponentsBuilt">Whether all components are built.</param>
    /// <param name="allAcceptanceCriteriaPassed">Whether all ACs pass.</param>
    /// <returns>True if building is complete.</returns>
    bool CanAutoTransitionToComplete(bool allComponentsBuilt, bool allAcceptanceCriteriaPassed);
}
