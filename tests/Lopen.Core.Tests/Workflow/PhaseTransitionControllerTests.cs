using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public sealed class PhaseTransitionControllerTests
{
    private readonly PhaseTransitionController _controller =
        new(NullLogger<PhaseTransitionController>.Instance);

    [Fact]
    public void HumanGate_InitiallyNotApproved()
    {
        Assert.False(_controller.IsRequirementGatheringToPlannningApproved);
    }

    [Fact]
    public void ApproveSpecification_SetsApproved()
    {
        _controller.ApproveSpecification();
        Assert.True(_controller.IsRequirementGatheringToPlannningApproved);
    }

    [Fact]
    public void ResetApproval_ClearsApproved()
    {
        _controller.ApproveSpecification();
        _controller.ResetApproval();
        Assert.False(_controller.IsRequirementGatheringToPlannningApproved);
    }

    [Fact]
    public void CanAutoTransitionToBuilding_BothTrue_ReturnsTrue()
    {
        Assert.True(_controller.CanAutoTransitionToBuilding(
            hasComponentsIdentified: true, hasTasksBreakdown: true));
    }

    [Fact]
    public void CanAutoTransitionToBuilding_NoComponents_ReturnsFalse()
    {
        Assert.False(_controller.CanAutoTransitionToBuilding(
            hasComponentsIdentified: false, hasTasksBreakdown: true));
    }

    [Fact]
    public void CanAutoTransitionToBuilding_NoTasks_ReturnsFalse()
    {
        Assert.False(_controller.CanAutoTransitionToBuilding(
            hasComponentsIdentified: true, hasTasksBreakdown: false));
    }

    [Fact]
    public void CanAutoTransitionToBuilding_BothFalse_ReturnsFalse()
    {
        Assert.False(_controller.CanAutoTransitionToBuilding(
            hasComponentsIdentified: false, hasTasksBreakdown: false));
    }

    [Fact]
    public void CanAutoTransitionToComplete_BothTrue_ReturnsTrue()
    {
        Assert.True(_controller.CanAutoTransitionToComplete(
            allComponentsBuilt: true, allAcceptanceCriteriaPassed: true));
    }

    [Fact]
    public void CanAutoTransitionToComplete_NotAllComponents_ReturnsFalse()
    {
        Assert.False(_controller.CanAutoTransitionToComplete(
            allComponentsBuilt: false, allAcceptanceCriteriaPassed: true));
    }

    [Fact]
    public void CanAutoTransitionToComplete_NotAllACs_ReturnsFalse()
    {
        Assert.False(_controller.CanAutoTransitionToComplete(
            allComponentsBuilt: true, allAcceptanceCriteriaPassed: false));
    }

    [Fact]
    public void CanAutoTransitionToComplete_BothFalse_ReturnsFalse()
    {
        Assert.False(_controller.CanAutoTransitionToComplete(
            allComponentsBuilt: false, allAcceptanceCriteriaPassed: false));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PhaseTransitionController(null!));
    }
}
