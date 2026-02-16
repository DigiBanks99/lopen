using System.Text.Json;
using Lopen.Core.Documents;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.ToolHandlers;

public class ToolHandlerBinderTests
{
    private readonly StubFileSystem _fileSystem = new();
    private readonly StubSectionExtractor _sectionExtractor = new();
    private readonly StubWorkflowEngine _engine = new();
    private readonly StubVerificationTracker _verificationTracker = new();
    private const string ProjectRoot = "/test/project";

    private ToolHandlerBinder CreateBinder() => new(
        _fileSystem, _sectionExtractor, _engine, _verificationTracker,
        NullLogger<ToolHandlerBinder>.Instance, ProjectRoot);

    [Fact]
    public void BindAll_BindsAllTenTools()
    {
        var registry = new TrackingToolRegistry();
        var binder = CreateBinder();

        binder.BindAll(registry);

        Assert.Equal(10, registry.BoundHandlers.Count);
        Assert.Contains("read_spec", registry.BoundHandlers.Keys);
        Assert.Contains("read_research", registry.BoundHandlers.Keys);
        Assert.Contains("read_plan", registry.BoundHandlers.Keys);
        Assert.Contains("update_task_status", registry.BoundHandlers.Keys);
        Assert.Contains("get_current_context", registry.BoundHandlers.Keys);
        Assert.Contains("log_research", registry.BoundHandlers.Keys);
        Assert.Contains("report_progress", registry.BoundHandlers.Keys);
        Assert.Contains("verify_task_completion", registry.BoundHandlers.Keys);
        Assert.Contains("verify_component_completion", registry.BoundHandlers.Keys);
        Assert.Contains("verify_module_completion", registry.BoundHandlers.Keys);
    }

    [Fact]
    public void BindAll_ThrowsOnNullRegistry()
    {
        var binder = CreateBinder();
        Assert.Throws<ArgumentNullException>(() => binder.BindAll(null!));
    }

    [Fact]
    public async Task HandleReadSpec_ReturnsSpecContent()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/SPECIFICATION.md"] = "# Core Spec\nContent here";
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("""{"module":"core"}""", CancellationToken.None);
        Assert.Contains("Core Spec", result);
    }

    [Fact]
    public async Task HandleReadSpec_ReturnsErrorWhenNotFound()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("""{"module":"missing"}""", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleReadSpec_ExtractsSectionWhenSpecified()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/SPECIFICATION.md"] = "# Spec\n## Overview\nHello";
        _sectionExtractor.Sections = [new ExtractedSection("Overview", "Hello", 1)];
        var binder = CreateBinder();

        var result = await binder.HandleReadSpec("""{"module":"core","section":"Overview"}""", CancellationToken.None);
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task HandleReadResearch_ReturnsMainResearch()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/RESEARCH.md"] = "# Research";
        _fileSystem.Directories.Add("/test/project/docs/requirements/core");
        var binder = CreateBinder();

        var result = await binder.HandleReadResearch("""{"module":"core"}""", CancellationToken.None);
        Assert.Contains("Research", result);
    }

    [Fact]
    public async Task HandleReadResearch_ReturnsTopicResearch()
    {
        _fileSystem.Files["/test/project/docs/requirements/core/RESEARCH-api.md"] = "# API Research";
        _fileSystem.Directories.Add("/test/project/docs/requirements/core");
        var binder = CreateBinder();

        var result = await binder.HandleReadResearch("""{"module":"core","topic":"api"}""", CancellationToken.None);
        Assert.Contains("API Research", result);
    }

    [Fact]
    public async Task HandleReadPlan_ReturnsPlanContent()
    {
        _fileSystem.Files["/test/project/docs/requirements/IMPLEMENTATION_PLAN.md"] = "# Plan";
        var binder = CreateBinder();

        var result = await binder.HandleReadPlan("", CancellationToken.None);
        Assert.Contains("Plan", result);
    }

    [Fact]
    public async Task HandleReadPlan_ReturnsErrorWhenMissing()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReadPlan("", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_RejectsCompleteWithoutVerification()
    {
        _verificationTracker.VerifiedItems.Clear();
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete"}""", CancellationToken.None);
        Assert.Contains("error", result);
        Assert.Contains("verify_task_completion", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_AcceptsCompleteWithVerification()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-1"));
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete"}""", CancellationToken.None);
        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_AcceptsNonCompleteStatus()
    {
        var binder = CreateBinder();

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"in-progress"}""", CancellationToken.None);
        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_ReturnsErrorOnMissingParams()
    {
        var binder = CreateBinder();
        var result = await binder.HandleUpdateTaskStatus("", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_CommitsOnCompletionWhenGitServiceProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-1"));
        var gitService = new StubGitWorkflowService();
        var binder = new ToolHandlerBinder(
            _fileSystem, _sectionExtractor, _engine, _verificationTracker,
            NullLogger<ToolHandlerBinder>.Instance, ProjectRoot, gitService);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"complete","module":"core","component":"workflow"}""",
            CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Single(gitService.Commits);
        Assert.Equal("core", gitService.Commits[0].Module);
        Assert.Equal("workflow", gitService.Commits[0].Component);
        Assert.Equal("task-1", gitService.Commits[0].Task);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_SkipsCommitWhenModuleNotProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-2"));
        var gitService = new StubGitWorkflowService();
        var binder = new ToolHandlerBinder(
            _fileSystem, _sectionExtractor, _engine, _verificationTracker,
            NullLogger<ToolHandlerBinder>.Instance, ProjectRoot, gitService);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-2","status":"complete"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Empty(gitService.Commits);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_SkipsCommitWhenGitServiceNotProvided()
    {
        _verificationTracker.VerifiedItems.Add(("Task", "task-3"));
        var binder = CreateBinder(); // No git service

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-3","status":"complete","module":"core"}""", CancellationToken.None);

        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleUpdateTaskStatus_DoesNotCommitForNonCompleteStatus()
    {
        var gitService = new StubGitWorkflowService();
        var binder = new ToolHandlerBinder(
            _fileSystem, _sectionExtractor, _engine, _verificationTracker,
            NullLogger<ToolHandlerBinder>.Instance, ProjectRoot, gitService);

        var result = await binder.HandleUpdateTaskStatus(
            """{"task_id":"task-1","status":"in-progress","module":"core"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Empty(gitService.Commits);
    }

    [Fact]
    public async Task HandleGetCurrentContext_ReturnsWorkflowState()
    {
        _engine.CurrentStep = WorkflowStep.IdentifyComponents;
        var binder = CreateBinder();

        var result = await binder.HandleGetCurrentContext("", CancellationToken.None);
        Assert.Contains("IdentifyComponents", result);
        Assert.Contains("Planning", result);
    }

    [Fact]
    public async Task HandleLogResearch_WritesFile()
    {
        var binder = CreateBinder();

        var result = await binder.HandleLogResearch(
            """{"module":"core","topic":"api","content":"# API findings"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.True(_fileSystem.Files.ContainsKey("/test/project/docs/requirements/core/RESEARCH-api.md"));
    }

    [Fact]
    public async Task HandleLogResearch_ReturnsErrorOnEmptyContent()
    {
        var binder = CreateBinder();
        var result = await binder.HandleLogResearch("""{"module":"core","topic":"api","content":""}""", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleReportProgress_ReturnsSuccess()
    {
        var binder = CreateBinder();
        var result = await binder.HandleReportProgress("""{"summary":"Made progress"}""", CancellationToken.None);
        Assert.Contains("success", result);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_RecordsVerification()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyTaskCompletion("""{"task_id":"task-1"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains(("Task", "task-1"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyTaskCompletion_ReturnsErrorOnMissingId()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyTaskCompletion("", CancellationToken.None);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task HandleVerifyComponentCompletion_RecordsVerification()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyComponentCompletion("""{"component_id":"comp-1"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains(("Component", "comp-1"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleVerifyModuleCompletion_RecordsVerification()
    {
        var binder = CreateBinder();
        var result = await binder.HandleVerifyModuleCompletion("""{"module_id":"core"}""", CancellationToken.None);
        Assert.Contains("success", result);
        Assert.Contains(("Module", "core"), _verificationTracker.RecordedVerifications);
    }

    [Fact]
    public async Task HandleReadSpec_HandlesInvalidJson()
    {
        _fileSystem.Files["/test/project/docs/requirements/DraftSpecification/SPECIFICATION.md"] = "# Spec";
        var binder = CreateBinder();
        var result = await binder.HandleReadSpec("not json", CancellationToken.None);
        Assert.NotNull(result);
    }

    // --- Stubs ---

    private sealed class StubGitWorkflowService : Lopen.Core.Git.IGitWorkflowService
    {
        public List<(string Module, string Component, string Task)> Commits { get; } = [];
        public Lopen.Core.Git.GitResult? CommitResult { get; set; } = new(0, "committed", "");

        public Task<Lopen.Core.Git.GitResult?> EnsureModuleBranchAsync(string moduleName, CancellationToken ct = default) =>
            Task.FromResult<Lopen.Core.Git.GitResult?>(new(0, "branch created", ""));

        public Task<Lopen.Core.Git.GitResult?> CommitTaskCompletionAsync(string moduleName, string componentName, string taskName, CancellationToken ct = default)
        {
            Commits.Add((moduleName, componentName, taskName));
            return Task.FromResult(CommitResult);
        }

        public string FormatCommitMessage(string moduleName, string componentName, string taskName) =>
            $"feat({moduleName}): complete {taskName}";
    }

    private sealed class StubFileSystem : Lopen.Storage.IFileSystem
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CreatedDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public bool FileExists(string path) => Files.ContainsKey(path);
        public bool DirectoryExists(string path) => Directories.Contains(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
            Files.TryGetValue(path, out var content)
                ? Task.FromResult(content)
                : Task.FromException<string>(new FileNotFoundException(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
        {
            Files[path] = content;
            return Task.CompletedTask;
        }
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string source, string dest) { }
        public void DeleteFile(string path) => Files.Remove(path);
        public void DeleteDirectory(string path, bool recursive = true) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }

    private sealed class StubSectionExtractor : ISectionExtractor
    {
        public IReadOnlyList<ExtractedSection> Sections { get; set; } = [];

        public IReadOnlyList<ExtractedSection> ExtractRelevantSections(string specContent, IReadOnlyList<string> relevantHeaders) =>
            Sections;

        public IReadOnlyList<ExtractedSection> ExtractAllSections(string specContent) => Sections;
    }

    private sealed class StubWorkflowEngine : IWorkflowEngine
    {
        public WorkflowStep CurrentStep { get; set; } = WorkflowStep.DraftSpecification;
        public WorkflowPhase CurrentPhase => CurrentStep switch
        {
            WorkflowStep.DraftSpecification => WorkflowPhase.RequirementGathering,
            WorkflowStep.IterateThroughTasks or WorkflowStep.Repeat => WorkflowPhase.Building,
            _ => WorkflowPhase.Planning
        };
        public bool IsComplete { get; set; }

        public Task InitializeAsync(string moduleName, CancellationToken ct = default) => Task.CompletedTask;
        public bool Fire(WorkflowTrigger trigger) => true;
        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() => [WorkflowTrigger.Assess];
    }

    private sealed class StubVerificationTracker : IVerificationTracker
    {
        public HashSet<(string Scope, string Id)> VerifiedItems { get; } = [];
        public List<(string Scope, string Id)> RecordedVerifications { get; } = [];

        public void RecordVerification(VerificationScope scope, string identifier, bool passed)
        {
            RecordedVerifications.Add((scope.ToString(), identifier));
            if (passed) VerifiedItems.Add((scope.ToString(), identifier));
        }

        public bool IsVerified(VerificationScope scope, string identifier) =>
            VerifiedItems.Contains((scope.ToString(), identifier));

        public void ResetForInvocation() => VerifiedItems.Clear();
    }

    private sealed class TrackingToolRegistry : IToolRegistry
    {
        public Dictionary<string, Func<string, CancellationToken, Task<string>>> BoundHandlers { get; } = [];

        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase) => [];
        public void RegisterTool(LopenToolDefinition tool) { }
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => [];
        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler)
        {
            BoundHandlers[toolName] = handler;
            return true;
        }
    }
}
