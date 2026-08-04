using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public sealed class WorkflowEngineTests
{
    private static WorkflowEngine CreateEngine(WorkflowStep initialStep = WorkflowStep.DraftSpecification)
    {
        var assessor = new FakeStateAssessor(initialStep);
        return new WorkflowEngine(assessor, NullLogger<WorkflowEngine>.Instance);
    }

    [Fact]
    public void InitialStep_IsDraftSpecification()
    {
        WorkflowEngine engine = CreateEngine();
        Assert.Equal(WorkflowStep.DraftSpecification, engine.CurrentStep);
    }

    [Fact]
    public void InitialPhase_IsRequirementGathering()
    {
        WorkflowEngine engine = CreateEngine();
        Assert.Equal(WorkflowPhase.RequirementGathering, engine.CurrentPhase);
    }

    [Fact]
    public void IsComplete_InitiallyFalse()
    {
        WorkflowEngine engine = CreateEngine();
        Assert.False(engine.IsComplete);
    }

    [Fact]
    public void Fire_SpecApproved_TransitionsToDetermineDependencies()
    {
        WorkflowEngine engine = CreateEngine();
        var result = engine.Fire(WorkflowTrigger.SpecApproved);

        Assert.True(result);
        Assert.Equal(WorkflowStep.DetermineDependencies, engine.CurrentStep);
    }

    [Fact]
    public void Fire_FullPath_DraftToRepeat()
    {
        WorkflowEngine engine = CreateEngine();

        Assert.True(engine.Fire(WorkflowTrigger.SpecApproved));
        Assert.Equal(WorkflowStep.DetermineDependencies, engine.CurrentStep);

        Assert.True(engine.Fire(WorkflowTrigger.DependenciesDetermined));
        Assert.Equal(WorkflowStep.IdentifyComponents, engine.CurrentStep);

        Assert.True(engine.Fire(WorkflowTrigger.ComponentsIdentified));
        Assert.Equal(WorkflowStep.SelectNextComponent, engine.CurrentStep);

        Assert.True(engine.Fire(WorkflowTrigger.ComponentSelected));
        Assert.Equal(WorkflowStep.BreakIntoTasks, engine.CurrentStep);

        Assert.True(engine.Fire(WorkflowTrigger.TasksBrokenDown));
        Assert.Equal(WorkflowStep.IterateThroughTasks, engine.CurrentStep);

        Assert.True(engine.Fire(WorkflowTrigger.ComponentComplete));
        Assert.Equal(WorkflowStep.Repeat, engine.CurrentStep);
    }

    [Fact]
    public void Fire_TaskIterationReentry()
    {
        WorkflowEngine engine = CreateEngine();
        engine.Fire(WorkflowTrigger.SpecApproved);
        engine.Fire(WorkflowTrigger.DependenciesDetermined);
        engine.Fire(WorkflowTrigger.ComponentsIdentified);
        engine.Fire(WorkflowTrigger.ComponentSelected);
        engine.Fire(WorkflowTrigger.TasksBrokenDown);

        Assert.Equal(WorkflowStep.IterateThroughTasks, engine.CurrentStep);

        // Can re-enter for task iterations
        Assert.True(engine.Fire(WorkflowTrigger.TaskIterationComplete));
        Assert.Equal(WorkflowStep.IterateThroughTasks, engine.CurrentStep);
    }

    [Fact]
    public void Fire_RepeatBackToSelect()
    {
        WorkflowEngine engine = CreateEngine();
        engine.Fire(WorkflowTrigger.SpecApproved);
        engine.Fire(WorkflowTrigger.DependenciesDetermined);
        engine.Fire(WorkflowTrigger.ComponentsIdentified);
        engine.Fire(WorkflowTrigger.ComponentSelected);
        engine.Fire(WorkflowTrigger.TasksBrokenDown);
        engine.Fire(WorkflowTrigger.ComponentComplete);

        Assert.Equal(WorkflowStep.Repeat, engine.CurrentStep);

        // Assess loops back to SelectNextComponent
        Assert.True(engine.Fire(WorkflowTrigger.Assess));
        Assert.Equal(WorkflowStep.SelectNextComponent, engine.CurrentStep);
    }

    [Fact]
    public void Fire_ModuleComplete_FromSelect_SetsIsComplete()
    {
        WorkflowEngine engine = CreateEngine();
        engine.Fire(WorkflowTrigger.SpecApproved);
        engine.Fire(WorkflowTrigger.DependenciesDetermined);
        engine.Fire(WorkflowTrigger.ComponentsIdentified);

        Assert.True(engine.Fire(WorkflowTrigger.ModuleComplete));
        Assert.Equal(WorkflowStep.Repeat, engine.CurrentStep);
        Assert.True(engine.IsComplete);
    }

    [Fact]
    public void Fire_InvalidTrigger_ReturnsFalse()
    {
        WorkflowEngine engine = CreateEngine();
        var result = engine.Fire(WorkflowTrigger.ComponentSelected);

        Assert.False(result);
        Assert.Equal(WorkflowStep.DraftSpecification, engine.CurrentStep);
    }

    [Fact]
    public void GetPermittedTriggers_AtDraft_ReturnsSpecApproved()
    {
        WorkflowEngine engine = CreateEngine();
        IReadOnlyList<WorkflowTrigger> triggers = engine.GetPermittedTriggers();

        Assert.Single(triggers);
        Assert.Contains(WorkflowTrigger.SpecApproved, triggers);
    }

    [Fact]
    public void GetPermittedTriggers_AtSelect_ReturnsComponentSelectedAndModuleComplete()
    {
        WorkflowEngine engine = CreateEngine();
        engine.Fire(WorkflowTrigger.SpecApproved);
        engine.Fire(WorkflowTrigger.DependenciesDetermined);
        engine.Fire(WorkflowTrigger.ComponentsIdentified);

        IReadOnlyList<WorkflowTrigger> triggers = engine.GetPermittedTriggers();

        Assert.Equal(2, triggers.Count);
        Assert.Contains(WorkflowTrigger.ComponentSelected, triggers);
        Assert.Contains(WorkflowTrigger.ModuleComplete, triggers);
    }

    [Fact]
    public void GetPermittedTriggers_AtIterate_ReturnsComponentCompleteAndTaskIteration()
    {
        WorkflowEngine engine = CreateEngine();
        engine.Fire(WorkflowTrigger.SpecApproved);
        engine.Fire(WorkflowTrigger.DependenciesDetermined);
        engine.Fire(WorkflowTrigger.ComponentsIdentified);
        engine.Fire(WorkflowTrigger.ComponentSelected);
        engine.Fire(WorkflowTrigger.TasksBrokenDown);

        IReadOnlyList<WorkflowTrigger> triggers = engine.GetPermittedTriggers();

        Assert.Equal(2, triggers.Count);
        Assert.Contains(WorkflowTrigger.ComponentComplete, triggers);
        Assert.Contains(WorkflowTrigger.TaskIterationComplete, triggers);
    }

    [Fact]
    public async Task InitializeAsync_SetsStepFromAssessor()
    {
        var assessor = new FakeStateAssessor(WorkflowStep.IdentifyComponents);
        var engine = new WorkflowEngine(assessor, NullLogger<WorkflowEngine>.Instance);

        await engine.InitializeAsync("test-module");

        Assert.Equal(WorkflowStep.IdentifyComponents, engine.CurrentStep);
        Assert.Equal(WorkflowPhase.Planning, engine.CurrentPhase);
    }

    [Fact]
    public async Task InitializeAsync_NullModule_Throws()
    {
        WorkflowEngine engine = CreateEngine();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.InitializeAsync(null!));
    }

    [Fact]
    public async Task InitializeAsync_EmptyModule_Throws()
    {
        WorkflowEngine engine = CreateEngine();
        await Assert.ThrowsAsync<ArgumentException>(
            () => engine.InitializeAsync(""));
    }

    [Theory]
    [InlineData(WorkflowStep.DraftSpecification, WorkflowPhase.RequirementGathering)]
    [InlineData(WorkflowStep.DetermineDependencies, WorkflowPhase.Planning)]
    [InlineData(WorkflowStep.IdentifyComponents, WorkflowPhase.Planning)]
    [InlineData(WorkflowStep.SelectNextComponent, WorkflowPhase.Planning)]
    [InlineData(WorkflowStep.BreakIntoTasks, WorkflowPhase.Planning)]
    [InlineData(WorkflowStep.IterateThroughTasks, WorkflowPhase.Building)]
    [InlineData(WorkflowStep.Repeat, WorkflowPhase.Building)]
    public void MapStepToPhase_CorrectMapping(WorkflowStep step, WorkflowPhase expectedPhase)
    {
        Assert.Equal(expectedPhase, WorkflowEngine.MapStepToPhase(step));
    }

    [Fact]
    public void Constructor_NullAssessor_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkflowEngine(null!, NullLogger<WorkflowEngine>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkflowEngine(new FakeStateAssessor(WorkflowStep.DraftSpecification), null!));
    }

    /// <summary>
    /// Simple fake that returns a configured initial step.
    /// </summary>
    private sealed class FakeStateAssessor : IStateAssessor
    {
        private readonly WorkflowStep _step;

        public FakeStateAssessor(WorkflowStep step) => _step = step;

        public Task<WorkflowAssessment> AssessAsync(string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkflowAssessment(_step, IsSpecReady: true, HasMoreComponents: false));
    }
}
