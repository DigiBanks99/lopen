using System.Text.Json;
using Lopen.Llm.Tools;

namespace Lopen.Llm.Tests.Tools;

public sealed class WorkflowOperationsTests
{
    private const string ProjectRoot = "/project";
    private const string PlanPath = "/project/docs/requirements/IMPLEMENTATION_PLAN.md";

    [Fact]
    public async Task ReadPlan_ReturnsContent_WhenPlanExists()
    {
        var fs = new FakeFileSystem();
        fs.AddFile(PlanPath, "# Plan\n- [ ] Task 1");

        var result = await WorkflowOperations.ReadPlan(fs, ProjectRoot);

        Assert.Equal("# Plan\n- [ ] Task 1", result);
    }

    [Fact]
    public async Task ReadPlan_ReturnsError_WhenNoPlan()
    {
        var fs = new FakeFileSystem();

        var result = await WorkflowOperations.ReadPlan(fs, ProjectRoot);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("plan", doc.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateTaskStatus_SucceedsForNonComplete()
    {
        var tracker = new FakeVerificationTracker();

        var result = await WorkflowOperations.UpdateTaskStatus(
            null, tracker, null, "task-1", "in-progress", null, null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("in-progress", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UpdateTaskStatus_EnforcesGate_WhenCompletionAttempted()
    {
        var gate = new FakeTaskStatusGate(TaskStatusGateResult.Allowed());
        var tracker = new FakeVerificationTracker();

        var result = await WorkflowOperations.UpdateTaskStatus(
            gate, tracker, null, "task-1", "complete", null, null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateTaskStatus_RejectsComplete_WhenGateFails()
    {
        var gate = new FakeTaskStatusGate(TaskStatusGateResult.Rejected("Not verified"));
        var tracker = new FakeVerificationTracker();

        var result = await WorkflowOperations.UpdateTaskStatus(
            gate, tracker, null, "task-1", "complete", null, null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("Not verified", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UpdateTaskStatus_RejectsComplete_WhenNotVerified_NoGate()
    {
        var tracker = new FakeVerificationTracker(); // IsVerified returns false

        var result = await WorkflowOperations.UpdateTaskStatus(
            null, tracker, null, "task-1", "complete", null, null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("verify_task_completion", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UpdateTaskStatus_UpdatesPlanCheckbox_WhenComplete()
    {
        var tracker = new FakeVerificationTracker { VerifiedAll = true };
        var planManager = new FakePlanManager();

        var result = await WorkflowOperations.UpdateTaskStatus(
            null, tracker, planManager, "task-1", "complete", "auth", null);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.True(planManager.WasUpdated);
        Assert.Equal("auth", planManager.LastModule);
        Assert.Equal("task-1", planManager.LastTaskText);
    }

    [Fact]
    public async Task GetCurrentContext_ReturnsJsonSnapshot()
    {
        var engine = new FakeWorkflowEngine
        {
            CurrentStep = "draft-spec",
            CurrentPhase = WorkflowPhase.RequirementGathering,
            IsComplete = false,
            PermittedTriggers = ["next", "abort"]
        };

        var result = await WorkflowOperations.GetCurrentContext(engine);

        var doc = JsonDocument.Parse(result);
        Assert.Equal("draft-spec", doc.RootElement.GetProperty("step").GetString());
        Assert.Equal("RequirementGathering", doc.RootElement.GetProperty("phase").GetString());
        Assert.Equal("False", doc.RootElement.GetProperty("is_complete").GetString());
        Assert.Contains("next", doc.RootElement.GetProperty("permitted_triggers").GetString());
    }

    [Fact]
    public async Task ReportProgress_ReturnsSuccess()
    {
        var result = await WorkflowOperations.ReportProgress("Did some work");

        var doc = JsonDocument.Parse(result);
        Assert.Equal("success", doc.RootElement.GetProperty("status").GetString());
        Assert.Contains("Did some work", doc.RootElement.GetProperty("message").GetString());
    }

    #region Fakes

    private sealed class FakeVerificationTracker : IVerificationTracker
    {
        public bool VerifiedAll { get; set; }

        public void RecordVerification(VerificationScope scope, string identifier, bool passed) { }

        public bool IsVerified(VerificationScope scope, string identifier) => VerifiedAll;

        public void ResetForInvocation() { }
    }

    private sealed class FakeTaskStatusGate(TaskStatusGateResult result) : ITaskStatusGate
    {
        public TaskStatusGateResult ValidateCompletion(VerificationScope scope, string identifier) => result;
    }

    private sealed class FakePlanManager : Storage.IPlanManager
    {
        public bool WasUpdated { get; private set; }
        public string? LastModule { get; private set; }
        public string? LastTaskText { get; private set; }

        public Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken ct = default)
        {
            WasUpdated = true;
            LastModule = module;
            LastTaskText = taskText;
            return Task.FromResult(true);
        }

        public Task WritePlanAsync(string module, string content, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<string?> ReadPlanAsync(string module, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> PlanExistsAsync(string module, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<Storage.PlanTask>> ReadTasksAsync(string module, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Storage.PlanTask>>(Array.Empty<Storage.PlanTask>());
    }

    private sealed class FakeWorkflowEngine : IToolWorkflowEngine
    {
        public string CurrentStep { get; set; } = "unknown";
        public WorkflowPhase CurrentPhase { get; set; } = WorkflowPhase.Building;
        public bool IsComplete { get; set; }
        public List<string> PermittedTriggers { get; set; } = [];

        IReadOnlyList<string> IToolWorkflowEngine.GetPermittedTriggers() => PermittedTriggers;
    }

    #endregion
}
