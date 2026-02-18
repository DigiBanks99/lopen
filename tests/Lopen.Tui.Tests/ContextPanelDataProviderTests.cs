using Lopen.Core.Documents;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

public sealed class ContextPanelDataProviderTests
{
    private readonly FakePlanManager _planManager = new();
    private readonly FakeWorkflowEngine _workflowEngine = new();

    private ContextPanelDataProvider CreateProvider() =>
        new(
            _planManager,
            _workflowEngine,
            NullLogger<ContextPanelDataProvider>.Instance);

    // --- Constructor validation ---

    [Fact]
    public void Constructor_ThrowsOnNullPlanManager()
    {
        Assert.Throws<ArgumentNullException>(() => new ContextPanelDataProvider(
            null!, _workflowEngine, NullLogger<ContextPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullWorkflowEngine()
    {
        Assert.Throws<ArgumentNullException>(() => new ContextPanelDataProvider(
            _planManager, null!, NullLogger<ContextPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new ContextPanelDataProvider(
            _planManager, _workflowEngine, null!));
    }

    // --- SetActiveModule ---

    [Fact]
    public void SetActiveModule_ThrowsOnNull()
    {
        var provider = CreateProvider();
        Assert.Throws<ArgumentNullException>(() => provider.SetActiveModule(null!));
    }

    [Fact]
    public void SetActiveModule_ThrowsOnWhitespace()
    {
        var provider = CreateProvider();
        Assert.Throws<ArgumentException>(() => provider.SetActiveModule("  "));
    }

    // --- GetCurrentData without module ---

    [Fact]
    public void GetCurrentData_WithoutModule_ReturnsEmptyData()
    {
        var provider = CreateProvider();
        var data = provider.GetCurrentData();

        Assert.Null(data.CurrentTask);
        Assert.Null(data.Component);
        Assert.Null(data.Module);
        Assert.Empty(data.Resources);
    }

    // --- GetCurrentData without refresh ---

    [Fact]
    public void GetCurrentData_WithModuleButNoRefresh_ReturnsEmptyData()
    {
        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        var data = provider.GetCurrentData();

        Assert.Null(data.CurrentTask);
        Assert.Null(data.Component);
        Assert.Null(data.Module);
    }

    // --- RefreshAsync ---

    [Fact]
    public async Task RefreshAsync_WithoutModule_DoesNotCallPlanManager()
    {
        var provider = CreateProvider();
        await provider.RefreshAsync();

        Assert.Equal(0, _planManager.ReadTasksCallCount);
    }

    [Fact]
    public async Task RefreshAsync_WithModule_ReadsTasks()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component A", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Task 1", IsCompleted = false, Level = 1 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();

        Assert.Equal(1, _planManager.ReadTasksCallCount);
        Assert.Equal("auth", _planManager.LastModuleRead);
    }

    [Fact]
    public async Task RefreshAsync_OnFailure_KeepsStaleCache()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component A", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();

        // Now make it fail
        _planManager.ShouldThrow = true;
        await provider.RefreshAsync();

        // Should still have data from the first refresh
        var data = provider.GetCurrentData();
        Assert.NotNull(data.Module);
    }

    // --- GetCurrentData with plan data ---

    [Fact]
    public async Task GetCurrentData_WithSingleComponent_ReturnsModuleSection()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "JWT Validation", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Module);
        Assert.Equal("auth", data.Module!.Name);
        Assert.Equal(1, data.Module.TotalComponents);
        Assert.Single(data.Module.Components);
        Assert.Equal("JWT Validation", data.Module.Components[0].Name);
    }

    [Fact]
    public async Task GetCurrentData_WithCompletedComponent_ShowsComplete()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Token Storage", IsCompleted = true, Level = 0 },
            new PlanTask { Text = "Store tokens", IsCompleted = true, Level = 1 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Module);
        Assert.Single(data.Module!.Components);
        Assert.Equal(TaskState.Complete, data.Module.Components[0].State);
    }

    [Fact]
    public async Task GetCurrentData_WithMultipleComponents_ShowsAll()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Token Storage", IsCompleted = true, Level = 0 },
            new PlanTask { Text = "JWT Validation", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "OAuth Flow", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Module);
        Assert.Equal(3, data.Module!.TotalComponents);
        Assert.Equal(TaskState.Complete, data.Module.Components[0].State);
        // First incomplete component should be marked InProgress
        Assert.Equal(TaskState.InProgress, data.Module.Components[1].State);
        Assert.Equal(TaskState.Pending, data.Module.Components[2].State);
    }

    [Fact]
    public async Task GetCurrentData_WithTasksUnderComponent_ShowsComponentSection()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "JWT Validation", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Parse token header", IsCompleted = true, Level = 1 },
            new PlanTask { Text = "Validate signature", IsCompleted = false, Level = 1 },
            new PlanTask { Text = "Check expiry", IsCompleted = false, Level = 1 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Component);
        Assert.Equal("JWT Validation", data.Component!.Name);
        Assert.Equal(1, data.Component.CompletedTasks);
        Assert.Equal(3, data.Component.TotalTasks);
        Assert.Equal(3, data.Component.Tasks.Count);
        Assert.Equal(TaskState.Complete, data.Component.Tasks[0].State);
        Assert.Equal(TaskState.InProgress, data.Component.Tasks[1].State);
        Assert.Equal(TaskState.Pending, data.Component.Tasks[2].State);
    }

    [Fact]
    public async Task GetCurrentData_WithSubtasksUnderTask_ShowsTaskSection()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "JWT Validation", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Validate signature", IsCompleted = false, Level = 1 },
            new PlanTask { Text = "Load public key", IsCompleted = true, Level = 2 },
            new PlanTask { Text = "Verify RSA signature", IsCompleted = false, Level = 2 },
            new PlanTask { Text = "Check key rotation", IsCompleted = false, Level = 2 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.CurrentTask);
        Assert.Equal("Validate signature", data.CurrentTask!.Name);
        Assert.Equal(1, data.CurrentTask.CompletedSubtasks);
        Assert.Equal(3, data.CurrentTask.TotalSubtasks);
        Assert.Equal(33, data.CurrentTask.ProgressPercent); // 1/3 = 33%
        Assert.Equal(TaskState.Complete, data.CurrentTask.Subtasks[0].State);
        Assert.Equal(TaskState.InProgress, data.CurrentTask.Subtasks[1].State);
        Assert.Equal(TaskState.Pending, data.CurrentTask.Subtasks[2].State);
    }

    [Fact]
    public async Task GetCurrentData_FullHierarchy_PopulatesAllSections()
    {
        _planManager.Tasks["core"] =
        [
            new PlanTask { Text = "Component A", IsCompleted = true, Level = 0 },
            new PlanTask { Text = "Task A1", IsCompleted = true, Level = 1 },
            new PlanTask { Text = "Component B", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Task B1", IsCompleted = true, Level = 1 },
            new PlanTask { Text = "Task B2", IsCompleted = false, Level = 1 },
            new PlanTask { Text = "Subtask B2a", IsCompleted = true, Level = 2 },
            new PlanTask { Text = "Subtask B2b", IsCompleted = false, Level = 2 },
            new PlanTask { Text = "Task B3", IsCompleted = false, Level = 1 },
            new PlanTask { Text = "Component C", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("core");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        // Module section
        Assert.NotNull(data.Module);
        Assert.Equal("core", data.Module!.Name);
        Assert.Equal(3, data.Module.TotalComponents);
        Assert.Equal(TaskState.Complete, data.Module.Components[0].State);
        Assert.Equal(TaskState.InProgress, data.Module.Components[1].State);
        Assert.Equal(TaskState.Pending, data.Module.Components[2].State);

        // Component section (first incomplete = Component B)
        Assert.NotNull(data.Component);
        Assert.Equal("Component B", data.Component!.Name);
        Assert.Equal(1, data.Component.CompletedTasks);
        Assert.Equal(3, data.Component.TotalTasks);

        // Task section (first incomplete task in Component B = Task B2)
        Assert.NotNull(data.CurrentTask);
        Assert.Equal("Task B2", data.CurrentTask!.Name);
        Assert.Equal(1, data.CurrentTask.CompletedSubtasks);
        Assert.Equal(2, data.CurrentTask.TotalSubtasks);
        Assert.Equal(50, data.CurrentTask.ProgressPercent);
    }

    [Fact]
    public async Task GetCurrentData_AllComponentsComplete_ShowsNoComponentOrTask()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Token Storage", IsCompleted = true, Level = 0 },
            new PlanTask { Text = "Store tokens", IsCompleted = true, Level = 1 },
            new PlanTask { Text = "Validate tokens", IsCompleted = true, Level = 0 },
            new PlanTask { Text = "Check expiry", IsCompleted = true, Level = 1 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Module);
        Assert.Null(data.Component);
        Assert.Null(data.CurrentTask);
    }

    [Fact]
    public async Task GetCurrentData_ComponentWithNoTasks_NoComponentSection()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component A", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Module);
        Assert.Null(data.Component);
    }

    [Fact]
    public async Task GetCurrentData_TaskWithNoSubtasks_NoTaskSection()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component A", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Task 1", IsCompleted = false, Level = 1 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.NotNull(data.Component);
        Assert.Null(data.CurrentTask);
    }

    [Fact]
    public async Task GetCurrentData_EmptyPlan_ReturnsEmptyData()
    {
        _planManager.Tasks["auth"] = [];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Null(data.Module);
        Assert.Null(data.Component);
        Assert.Null(data.CurrentTask);
    }

    // --- Module switching ---

    [Fact]
    public async Task SetActiveModule_InvalidatesCacheAndSwitchesModule()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Auth Component", IsCompleted = false, Level = 0 },
        ];
        _planManager.Tasks["core"] =
        [
            new PlanTask { Text = "Core Component", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();

        var data1 = provider.GetCurrentData();
        Assert.Equal("auth", data1.Module!.Name);

        // Switch module
        provider.SetActiveModule("core");
        // Before refresh, cache is invalidated
        var data2 = provider.GetCurrentData();
        Assert.Null(data2.Module);

        // After refresh, shows new module
        await provider.RefreshAsync();
        var data3 = provider.GetCurrentData();
        Assert.Equal("core", data3.Module!.Name);
    }

    // --- Progress percentage ---

    [Fact]
    public async Task GetCurrentData_ProgressPercent_CalculatesCorrectly()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Task", IsCompleted = false, Level = 1 },
            new PlanTask { Text = "Sub 1", IsCompleted = true, Level = 2 },
            new PlanTask { Text = "Sub 2", IsCompleted = true, Level = 2 },
            new PlanTask { Text = "Sub 3", IsCompleted = false, Level = 2 },
            new PlanTask { Text = "Sub 4", IsCompleted = false, Level = 2 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Equal(50, data.CurrentTask!.ProgressPercent); // 2/4 = 50%
    }

    [Fact]
    public async Task GetCurrentData_AllSubtasksComplete_ProgressIs100()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Task 1", IsCompleted = true, Level = 1 },
            new PlanTask { Text = "Task 2", IsCompleted = false, Level = 1 },
            new PlanTask { Text = "Sub A", IsCompleted = true, Level = 2 },
            new PlanTask { Text = "Sub B", IsCompleted = true, Level = 2 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Equal(100, data.CurrentTask!.ProgressPercent);
    }

    // --- InProgress component detection ---

    [Fact]
    public async Task GetCurrentData_ComponentWithSomeChildrenComplete_ShowsInProgress()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component A", IsCompleted = false, Level = 0 },
            new PlanTask { Text = "Task 1", IsCompleted = true, Level = 1 },
            new PlanTask { Text = "Task 2", IsCompleted = false, Level = 1 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Equal(TaskState.InProgress, data.Module!.Components[0].State);
    }

    // --- Resources (currently empty) ---

    [Fact]
    public async Task GetCurrentData_Resources_IsEmptyByDefault()
    {
        _planManager.Tasks["auth"] =
        [
            new PlanTask { Text = "Component", IsCompleted = false, Level = 0 },
        ];

        var provider = CreateProvider();
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Empty(data.Resources);
    }

    // --- DI registration ---

    [Fact]
    public void AddContextPanelDataProvider_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPlanManager, FakePlanManager>();
        services.AddSingleton<IWorkflowEngine, FakeWorkflowEngine>();
        services.AddContextPanelDataProvider();

        using var provider = services.BuildServiceProvider();
        var dataProvider = provider.GetService<IContextPanelDataProvider>();

        Assert.NotNull(dataProvider);
        Assert.IsType<ContextPanelDataProvider>(dataProvider);
    }

    // --- BuildContextData static method tests ---

    [Fact]
    public void BuildContextData_WithEmptyTasks_ReturnsEmptyModuleSection()
    {
        var data = ContextPanelDataProvider.BuildContextData("mod", []);
        Assert.NotNull(data.Module);
        Assert.Equal(0, data.Module!.TotalComponents);
        Assert.Empty(data.Module.Components);
    }

    [Fact]
    public void BuildContextData_SkipsCompletedComponentsForDetailSections()
    {
        var tasks = new List<PlanTask>
        {
            new() { Text = "Done Component", IsCompleted = true, Level = 0 },
            new() { Text = "Done Task", IsCompleted = true, Level = 1 },
            new() { Text = "Active Component", IsCompleted = false, Level = 0 },
            new() { Text = "Active Task", IsCompleted = false, Level = 1 },
        };

        var data = ContextPanelDataProvider.BuildContextData("mod", tasks);

        Assert.Equal("Active Component", data.Component!.Name);
        Assert.Single(data.Component.Tasks);
    }

    // --- Integration test: TuiApplication accepts context provider ---

    [Fact]
    public void TuiApplication_AcceptsNullContextPanelDataProvider()
    {
        var app = new TuiApplication(
            new TopPanelComponent(), new ActivityPanelComponent(),
            new ContextPanelComponent(), new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            topPanelDataProvider: null,
            contextPanelDataProvider: null);
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void TuiApplication_AcceptsContextPanelDataProvider()
    {
        var ctxProvider = new StubContextPanelDataProvider();
        var app = new TuiApplication(
            new TopPanelComponent(), new ActivityPanelComponent(),
            new ContextPanelComponent(), new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            topPanelDataProvider: null,
            contextPanelDataProvider: ctxProvider);
        Assert.False(app.IsRunning);
    }

    // --- Resources with ResourceTracker ---

    [Fact]
    public async Task GetCurrentData_WithResourceTracker_IncludesResources()
    {
        var resourceTracker = new FakeResourceTracker();
        resourceTracker.Resources["auth"] = new List<TrackedResource>
        {
            new("SPECIFICATION.md", "/proj/docs/requirements/auth/SPECIFICATION.md", "spec content"),
            new("plan.md", "/proj/.lopen/modules/auth/plan.md", "plan content"),
        };

        var provider = new ContextPanelDataProvider(
            _planManager, _workflowEngine,
            NullLogger<ContextPanelDataProvider>.Instance,
            resourceTracker);

        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Equal(2, data.Resources.Count);
        Assert.Contains(data.Resources, r => r.Label == "SPECIFICATION.md");
        Assert.Contains(data.Resources, r => r.Label == "plan.md");
    }

    [Fact]
    public async Task RefreshAsync_WithResourceTracker_PopulatesResources()
    {
        var resourceTracker = new FakeResourceTracker();
        resourceTracker.Resources["auth"] = new List<TrackedResource>
        {
            new("RESEARCH.md", "/proj/docs/requirements/auth/RESEARCH.md", "research"),
        };

        var provider = new ContextPanelDataProvider(
            _planManager, _workflowEngine,
            NullLogger<ContextPanelDataProvider>.Instance,
            resourceTracker);

        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Single(data.Resources);
        Assert.Equal("RESEARCH.md", data.Resources[0].Label);
        Assert.Equal("research", data.Resources[0].Content);
        Assert.Equal(1, resourceTracker.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_WithNullResourceTracker_ResourcesEmpty()
    {
        var provider = CreateProvider(); // no resource tracker
        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Empty(data.Resources);
    }

    [Fact]
    public async Task RefreshAsync_ResourceTrackerThrows_KeepsPreviousResources()
    {
        var resourceTracker = new FakeResourceTracker();
        resourceTracker.Resources["auth"] = new List<TrackedResource>
        {
            new("SPECIFICATION.md", "/path", "content"),
        };

        var provider = new ContextPanelDataProvider(
            _planManager, _workflowEngine,
            NullLogger<ContextPanelDataProvider>.Instance,
            resourceTracker);

        provider.SetActiveModule("auth");
        await provider.RefreshAsync();
        var data1 = provider.GetCurrentData();
        Assert.Single(data1.Resources);

        // Now make tracker throw
        resourceTracker.ShouldThrow = true;
        await provider.RefreshAsync();
        var data2 = provider.GetCurrentData();

        // Previous resources should be kept
        Assert.Single(data2.Resources);
        Assert.Equal("SPECIFICATION.md", data2.Resources[0].Label);
    }

    [Fact]
    public void AddContextPanelDataProvider_WithResourceTracker_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPlanManager, FakePlanManager>();
        services.AddSingleton<IWorkflowEngine, FakeWorkflowEngine>();
        services.AddSingleton<IResourceTracker, FakeResourceTracker>();
        services.AddContextPanelDataProvider();

        using var sp = services.BuildServiceProvider();
        var dataProvider = sp.GetService<IContextPanelDataProvider>();

        Assert.NotNull(dataProvider);
        Assert.IsType<ContextPanelDataProvider>(dataProvider);
    }

    // --- Fakes ---

    private sealed class FakePlanManager : IPlanManager
    {
        public Dictionary<string, IReadOnlyList<PlanTask>> Tasks { get; } = new();
        public bool ShouldThrow { get; set; }
        public int ReadTasksCallCount { get; private set; }
        public string? LastModuleRead { get; private set; }

        public Task<IReadOnlyList<PlanTask>> ReadTasksAsync(string module, CancellationToken cancellationToken = default)
        {
            ReadTasksCallCount++;
            LastModuleRead = module;
            if (ShouldThrow)
                throw new InvalidOperationException("plan read failed");
            if (Tasks.TryGetValue(module, out var tasks))
                return Task.FromResult(tasks);
            return Task.FromResult<IReadOnlyList<PlanTask>>([]);
        }

        public Task WritePlanAsync(string module, string content, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<string?> ReadPlanAsync(string module, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
        public Task<bool> PlanExistsAsync(string module, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeWorkflowEngine : IWorkflowEngine
    {
        public WorkflowStep CurrentStep => WorkflowStep.DraftSpecification;
        public WorkflowPhase CurrentPhase => WorkflowPhase.RequirementGathering;
        public bool IsComplete => false;
        public Task InitializeAsync(string m, CancellationToken ct = default) => Task.CompletedTask;
        public bool Fire(WorkflowTrigger t) => true;
        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() => [];
    }

    private sealed class StubContextPanelDataProvider : IContextPanelDataProvider
    {
        public ContextPanelData GetCurrentData() => new();
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetActiveModule(string moduleName) { }
    }

    private sealed class FakeResourceTracker : IResourceTracker
    {
        public Dictionary<string, IReadOnlyList<TrackedResource>> Resources { get; } = new();
        public bool ShouldThrow { get; set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<TrackedResource>> GetActiveResourcesAsync(
            string moduleName, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (ShouldThrow)
                throw new InvalidOperationException("resource tracking failed");
            if (Resources.TryGetValue(moduleName, out var resources))
                return Task.FromResult(resources);
            return Task.FromResult<IReadOnlyList<TrackedResource>>([]);
        }
    }
}
