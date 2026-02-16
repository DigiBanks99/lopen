using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.Tasks;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests;

/// <summary>
/// Explicit acceptance criteria coverage tests for the Core module.
/// Each test maps to a numbered AC from docs/requirements/core/SPECIFICATION.md.
/// </summary>
public class CoreAcceptanceCriteriaTests
{
    // AC-01: Lopen scans docs/requirements/ and correctly identifies all module specifications

    [Fact]
    public void AC01_ModuleScanner_IdentifiesModulesWithSpecs()
    {
        var fs = new InMemoryFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/auth");
        fs.AddFile("/project/docs/requirements/auth/SPECIFICATION.md", "# Auth Module");
        fs.AddDirectory("/project/docs/requirements/llm");
        fs.AddFile("/project/docs/requirements/llm/SPECIFICATION.md", "# LLM Module");

        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");
        var modules = scanner.ScanModules();

        Assert.Equal(2, modules.Count);
        Assert.Contains(modules, m => m.Name == "auth" && m.HasSpecification);
        Assert.Contains(modules, m => m.Name == "llm" && m.HasSpecification);
    }

    // AC-02: The 7-step workflow executes in order

    [Fact]
    public void AC02_WorkflowEngine_SevenStepsInOrder()
    {
        var assessor = new FakeStateAssessor(WorkflowStep.DraftSpecification);
        var engine = new WorkflowEngine(assessor, NullLogger<WorkflowEngine>.Instance);

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

    // AC-03: Re-entrant assessment correctly determines current workflow step

    [Fact]
    public async Task AC03_Assessor_DeterminesStepFromCodebaseState()
    {
        var fs = new InMemoryFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/auth");
        fs.AddFile("/project/docs/requirements/auth/SPECIFICATION.md", "# Auth\n- [ ] task1\n- [x] task2");

        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");
        var assessor = new CodebaseStateAssessor(
            fs, scanner, NullLogger<CodebaseStateAssessor>.Instance);

        var step = await assessor.GetCurrentStepAsync("auth");
        // Has spec with incomplete checkboxes = in progress (not DraftSpecification, not Repeat)
        Assert.NotEqual(WorkflowStep.DraftSpecification, step);
    }

    // AC-04: Specification drift detection

    [Fact]
    public void AC04_DriftDetector_IdentifiesChangedSections()
    {
        var parser = new MarkdigSpecificationParser();
        var hasher = new XxHashContentHasher();
        var detector = new DriftDetector(parser, hasher, NullLogger<DriftDetector>.Instance);

        var cache = new[]
        {
            new CachedSection("spec.md", "Overview", "Original content\n",
                hasher.ComputeHash("Original content\n"), DateTimeOffset.UtcNow),
            new CachedSection("spec.md", "Auth", "Auth section",
                hasher.ComputeHash("Auth section"), DateTimeOffset.UtcNow),
        };

        var changed = "# Overview\nModified content\n# Auth\nAuth section";
        var drift = detector.DetectDrift("spec.md", changed, cache);

        Assert.Contains(drift, d => d.Header == "Overview");
    }

    // AC-05: Requirement Gathering → Planning requires human confirmation

    [Fact]
    public void AC05_HumanGate_RequiresExplicitApproval()
    {
        var controller = new PhaseTransitionController(
            NullLogger<PhaseTransitionController>.Instance);

        Assert.False(controller.IsRequirementGatheringToPlannningApproved);
        controller.ApproveSpecification();
        Assert.True(controller.IsRequirementGatheringToPlannningApproved);
        controller.ResetApproval();
        Assert.False(controller.IsRequirementGatheringToPlannningApproved);
    }

    // AC-06: Planning → Building proceeds automatically when plan is structurally complete

    [Fact]
    public void AC06_PlanningToBuilding_AutoTransition()
    {
        var controller = new PhaseTransitionController(
            NullLogger<PhaseTransitionController>.Instance);

        Assert.True(controller.CanAutoTransitionToBuilding(
            hasComponentsIdentified: true, hasTasksBreakdown: true));
        Assert.False(controller.CanAutoTransitionToBuilding(
            hasComponentsIdentified: false, hasTasksBreakdown: true));
    }

    // AC-07: Building → Complete proceeds automatically when all ACs pass

    [Fact]
    public void AC07_BuildingToComplete_AutoTransition()
    {
        var controller = new PhaseTransitionController(
            NullLogger<PhaseTransitionController>.Instance);

        Assert.True(controller.CanAutoTransitionToComplete(
            allComponentsBuilt: true, allAcceptanceCriteriaPassed: true));
        Assert.False(controller.CanAutoTransitionToComplete(
            allComponentsBuilt: true, allAcceptanceCriteriaPassed: false));
    }

    // AC-08: Task hierarchy supports Module → Component → Task → Subtask

    [Fact]
    public void AC08_TaskHierarchy_FourLevels()
    {
        var module = new ModuleNode("mod-1", "auth");
        var component = new ComponentNode("comp-1", "login");
        var task = new TaskNode("task-1", "implement-jwt");
        var subtask = new SubtaskNode("sub-1", "add-rs256-signing");

        task.AddChild(subtask);
        component.AddChild(task);
        module.AddChild(component);

        Assert.Single(module.Children);
        Assert.Single(component.Children);
        Assert.Single(task.Children);
        Assert.Empty(subtask.Children);
    }

    // AC-09: Task state transitions: Pending → In Progress → Complete/Failed

    [Fact]
    public void AC09_TaskStateTransitions_ValidPaths()
    {
        var node = new SubtaskNode("t1", "test-task");
        Assert.Equal(WorkNodeState.Pending, node.State);

        node.TransitionTo(WorkNodeState.InProgress);
        Assert.Equal(WorkNodeState.InProgress, node.State);

        node.TransitionTo(WorkNodeState.Complete);
        Assert.Equal(WorkNodeState.Complete, node.State);
    }

    [Fact]
    public void AC09_TaskStateTransitions_InvalidPath_Throws()
    {
        var node = new SubtaskNode("t1", "test-task");
        Assert.Throws<InvalidStateTransitionException>(() =>
            node.TransitionTo(WorkNodeState.Complete));
    }

    // AC-10: Task completion requires oracle verification

    [Fact]
    public async Task AC10_QualityGate_BlocksWithoutVerification()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: ctx => ctx.TaskName is not null,
            hasPassingVerification: _ => false);

        var ctx = new GuardrailContext("auth", "build-jwt", 1, 1);
        var result = await guardrail.EvaluateAsync(ctx);
        Assert.IsType<GuardrailResult.Block>(result);
    }

    [Fact]
    public async Task AC10_QualityGate_AllowsWithVerification()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: ctx => ctx.TaskName is not null,
            hasPassingVerification: _ => true);

        var ctx = new GuardrailContext("auth", "build-jwt", 1, 1);
        var result = await guardrail.EvaluateAsync(ctx);
        Assert.IsType<GuardrailResult.Pass>(result);
    }

    // AC-11: Back-pressure Category 1 (Resource Limits)

    [Fact]
    public async Task AC11_ResourceLimits_WarnsAtThreshold()
    {
        var tracker = new FakeTokenTracker { PremiumRequests = 6 };
        var guardrail = new ResourceLimitGuardrail(
            tracker,
            NullLogger<ResourceLimitGuardrail>.Instance,
            premiumRequestBudget: 10,
            warnThreshold: 0.5,
            blockThreshold: 0.9);

        var ctx = new GuardrailContext("auth", null, 1, 1);
        var result = await guardrail.EvaluateAsync(ctx);
        Assert.IsType<GuardrailResult.Warn>(result);
    }

    // AC-12: Back-pressure Category 2: churn detection

    [Fact]
    public async Task AC12_ChurnDetection_EscalatesAfterThreshold()
    {
        var guardrail = new ChurnDetectionGuardrail(failureThreshold: 3);

        var r1 = await guardrail.EvaluateAsync(new GuardrailContext("auth", "failing-task", 1, 1));
        Assert.IsType<GuardrailResult.Pass>(r1);

        var r2 = await guardrail.EvaluateAsync(new GuardrailContext("auth", "failing-task", 2, 1));
        Assert.IsType<GuardrailResult.Warn>(r2);

        var r3 = await guardrail.EvaluateAsync(new GuardrailContext("auth", "failing-task", 3, 1));
        Assert.IsType<GuardrailResult.Block>(r3);
    }

    // AC-13: False completion claims rejected

    [Fact]
    public async Task AC13_FalseCompletion_Rejected()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: ctx => ctx.TaskName is not null,
            hasPassingVerification: _ => false);

        var ctx = new GuardrailContext("auth", "fake-complete", 1, 1);
        var result = await guardrail.EvaluateAsync(ctx);

        Assert.IsType<GuardrailResult.Block>(result);
        var block = (GuardrailResult.Block)result;
        Assert.Contains("fake-complete", block.Message);
    }

    // AC-14: Quality gates enforced at module and component completion

    [Fact]
    public async Task AC14_QualityGates_EnforcedAtCompletion()
    {
        var guardrail = new QualityGateGuardrail(
            isCompletionBoundary: _ => true,
            hasPassingVerification: _ => false);

        var ctx = new GuardrailContext("auth-module", null, 1, 1);
        var result = await guardrail.EvaluateAsync(ctx);
        Assert.IsType<GuardrailResult.Block>(result);
    }

    // AC-15: Back-pressure Category 4: Tool Discipline

    [Fact]
    public async Task AC15_ToolDiscipline_WarnsOnWastefulPatterns()
    {
        var guardrail = new ToolDisciplineGuardrail(toolCallThreshold: 5);
        var ctx = new GuardrailContext("auth", null, 1, 10);
        var result = await guardrail.EvaluateAsync(ctx);

        Assert.IsType<GuardrailResult.Warn>(result);
    }

    // AC-16: Git auto-commit after task completion

    [Fact]
    public void AC16_GitWorkflow_FormatsCommitMessage()
    {
        var gitOptions = new GitOptions { Enabled = true };
        var gitService = new FakeGitService(success: true);
        var service = new GitWorkflowService(
            gitService, gitOptions, NullLogger<GitWorkflowService>.Instance);

        var message = service.FormatCommitMessage("auth", "login", "implement-jwt");
        Assert.Contains("auth", message);
        Assert.Contains("implement-jwt", message);
    }

    // AC-17: Branch per module

    [Fact]
    public void AC17_BranchPrefix_IsLopen()
    {
        Assert.Equal("lopen/", GitWorkflowService.BranchPrefix);
    }

    // AC-18: lopen revert rolls back to last task-completion commit

    [Fact]
    public async Task AC18_RevertService_ValidSha_Reverts()
    {
        var gitOptions = new GitOptions { Enabled = true };
        var gitService = new FakeGitService(success: true);
        var service = new RevertService(
            gitService, gitOptions, NullLogger<RevertService>.Instance);

        var result = await service.RevertToCommitAsync("abc123");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AC18_RevertService_GitDisabled_ReturnsFalse()
    {
        var gitOptions = new GitOptions { Enabled = false };
        var gitService = new FakeGitService(success: true);
        var service = new RevertService(
            gitService, gitOptions, NullLogger<RevertService>.Instance);

        var result = await service.RevertToCommitAsync("abc123");
        Assert.False(result.Success);
    }

    // AC-19: Document management extracts relevant sections

    [Fact]
    public void AC19_SectionExtractor_ExtractsRelevantSections()
    {
        var parser = new MarkdigSpecificationParser();
        var extractor = new SectionExtractor(parser, NullLogger<SectionExtractor>.Instance);

        var content = "# Overview\nThis is overview.\n# Authentication\nAuth details.\n# API\nAPI docs.";
        var sections = extractor.ExtractRelevantSections(content, ["Authentication"]);

        Assert.Single(sections);
        Assert.Contains("Auth details", sections[0].Content);
    }

    // AC-20: Programmatic updates without LLM invocation

    [Fact]
    public void AC20_MarkdownUpdater_TogglesCheckboxesWithoutLlm()
    {
        var content = "- [ ] Implement login\n- [x] Add tests";
        var updated = MarkdownUpdater.ToggleCheckbox(content, "Implement login", completed: true);
        Assert.Contains("- [x] Implement login", updated);
    }

    [Fact]
    public void AC20_MarkdownUpdater_CountsCheckboxes()
    {
        var content = "- [ ] task1\n- [x] task2\n- [x] task3";
        var (total, completed) = MarkdownUpdater.CountCheckboxes(content);
        Assert.Equal(3, total);
        Assert.Equal(2, completed);
    }

    // AC-21: Single task failures - LLM self-corrects

    [Fact]
    public void AC21_SingleFailure_ReturnsSelfCorrect()
    {
        var handler = new FailureHandler(NullLogger<FailureHandler>.Instance);
        var result = handler.RecordFailure("task-1", "Build failed");

        Assert.Equal(FailureSeverity.TaskFailure, result.Severity);
        Assert.Equal(FailureAction.SelfCorrect, result.Action);
    }

    // AC-22: Repeated task failures prompt user intervention

    [Fact]
    public void AC22_RepeatedFailures_PromptsUser()
    {
        var handler = new FailureHandler(NullLogger<FailureHandler>.Instance, failureThreshold: 3);
        handler.RecordFailure("task-1", "fail 1");
        handler.RecordFailure("task-1", "fail 2");
        var result = handler.RecordFailure("task-1", "fail 3");

        Assert.Equal(FailureSeverity.RepeatedFailure, result.Severity);
        Assert.Equal(FailureAction.PromptUser, result.Action);
    }

    // AC-23: Critical system errors block execution

    [Fact]
    public void AC23_CriticalError_BlocksExecution()
    {
        var handler = new FailureHandler(NullLogger<FailureHandler>.Instance);
        var result = handler.RecordCriticalError("Disk full");

        Assert.Equal(FailureSeverity.Critical, result.Severity);
        Assert.Equal(FailureAction.Block, result.Action);
    }

    // AC-24: Module selection lists modules with current state

    [Fact]
    public void AC24_ModuleLister_ListsModulesWithState()
    {
        var fs = new InMemoryFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/auth");
        fs.AddFile("/project/docs/requirements/auth/SPECIFICATION.md", "# Auth\n- [x] task1\n- [x] task2");
        fs.AddDirectory("/project/docs/requirements/llm");
        fs.AddFile("/project/docs/requirements/llm/SPECIFICATION.md", "# LLM\n- [ ] task1\n- [x] task2");

        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");
        var lister = new ModuleLister(scanner, fs, NullLogger<ModuleLister>.Instance);
        var modules = lister.ListModules();

        Assert.Equal(2, modules.Count);
        Assert.Contains(modules, m => m.Name == "auth" && m.Status == ModuleStatus.Complete);
        Assert.Contains(modules, m => m.Name == "llm" && m.Status == ModuleStatus.InProgress);
    }

    // -- Test helpers --

    private sealed class FakeStateAssessor : IStateAssessor
    {
        private readonly WorkflowStep _step;
        public FakeStateAssessor(WorkflowStep step) => _step = step;

        public Task<WorkflowStep> GetCurrentStepAsync(string moduleName, CancellationToken ct = default)
            => Task.FromResult(_step);

        public Task PersistStepAsync(string moduleName, WorkflowStep step, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> IsSpecReadyAsync(string moduleName, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> HasMoreComponentsAsync(string moduleName, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class FakeTokenTracker : ITokenTracker
    {
        public int PremiumRequests { get; set; }

        public void RecordUsage(TokenUsage usage) { }

        public SessionTokenMetrics GetSessionMetrics() =>
            new() { PremiumRequestCount = PremiumRequests };

        public void ResetSession() => PremiumRequests = 0;
    }

    private sealed class FakeGitService : IGitService
    {
        private readonly bool _success;
        public FakeGitService(bool success) => _success = success;

        public Task<GitResult> CommitAllAsync(string message, CancellationToken ct = default)
            => Task.FromResult(new GitResult(0, _success ? "ok" : "fail", ""));

        public Task<GitResult> CreateBranchAsync(string name, CancellationToken ct = default)
            => Task.FromResult(new GitResult(0, "ok", ""));

        public Task<GitResult> ResetToCommitAsync(string sha, CancellationToken ct = default)
            => Task.FromResult(_success
                ? new GitResult(0, "ok", "")
                : new GitResult(1, "", "error"));

        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken ct = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task<string> GetDiffAsync(CancellationToken ct = default)
            => Task.FromResult("diff");

        public Task<string?> GetCurrentCommitShaAsync(CancellationToken ct = default)
            => Task.FromResult<string?>("abc123");
    }
}
