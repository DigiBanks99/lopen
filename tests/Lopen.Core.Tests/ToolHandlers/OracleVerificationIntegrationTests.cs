using System.Text.Json;
using Lopen.Configuration;
using Lopen.Core.Documents;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.ToolHandlers;

/// <summary>
/// Integration tests for CORE-10: Oracle verification blocks premature task completion
/// across the full pipeline. Uses real ToolHandlerBinder, TaskStatusGate, VerificationTracker,
/// and OracleVerifier with a controllable FakeLlmService.
/// </summary>
public class OracleVerificationIntegrationTests
{
    private static readonly OracleOptions DefaultOracleOptions = new() { Model = "gpt-5-mini" };

    private static string OraclePass() => """{"pass": true, "gaps": []}""";
    private static string OracleFail() => """{"pass": false, "gaps": ["Missing test coverage", "No error handling"]}""";
    private static string OracleMalformed() => "not json at all";

    private static (TrackingToolRegistry registry, FakeLlmService llm, VerificationTracker tracker) CreatePipeline()
    {
        var tracker = new VerificationTracker();
        var gate = new TaskStatusGate(tracker, NullLogger<TaskStatusGate>.Instance);
        var llm = new FakeLlmService();
        var oracle = new OracleVerifier(llm, DefaultOracleOptions, NullLogger<OracleVerifier>.Instance);

        var binder = new ToolHandlerBinder(
            new StubFileSystem(),
            new StubSectionExtractor(),
            new StubWorkflowEngine(),
            tracker,
            NullLogger<ToolHandlerBinder>.Instance,
            "/tmp/test-project",
            taskStatusGate: gate,
            oracleVerifier: oracle);

        var registry = new TrackingToolRegistry();
        binder.BindAll(registry);
        return (registry, llm, tracker);
    }

    private static async Task<string> InvokeVerifyTask(TrackingToolRegistry registry, string taskId = "t1")
    {
        var handler = registry.BoundHandlers["verify_task_completion"];
        return await handler(
            JsonSerializer.Serialize(new { task_id = taskId, evidence = "diff output", acceptance_criteria = "tests pass" }),
            CancellationToken.None);
    }

    private static async Task<string> InvokeVerifyComponent(TrackingToolRegistry registry, string componentId = "c1")
    {
        var handler = registry.BoundHandlers["verify_component_completion"];
        return await handler(
            JsonSerializer.Serialize(new { component_id = componentId, evidence = "diff output", acceptance_criteria = "tests pass" }),
            CancellationToken.None);
    }

    private static async Task<string> InvokeVerifyModule(TrackingToolRegistry registry, string moduleId = "m1")
    {
        var handler = registry.BoundHandlers["verify_module_completion"];
        return await handler(
            JsonSerializer.Serialize(new { module_id = moduleId, evidence = "diff output", acceptance_criteria = "tests pass" }),
            CancellationToken.None);
    }

    private static async Task<string> InvokeUpdateTaskStatus(TrackingToolRegistry registry, string taskId = "t1", string status = "complete")
    {
        var handler = registry.BoundHandlers["update_task_status"];
        return await handler(
            JsonSerializer.Serialize(new { task_id = taskId, status }),
            CancellationToken.None);
    }

    private static string GetStatus(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("status").GetString()!;
    }

    [Fact]
    public async Task FullPipeline_OraclePass_ThenTaskComplete_Accepted()
    {
        var (registry, llm, _) = CreatePipeline();
        llm.NextResponse = OraclePass();

        var verifyResult = await InvokeVerifyTask(registry);
        Assert.Equal("success", GetStatus(verifyResult));

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("success", GetStatus(updateResult));
    }

    [Fact]
    public async Task FullPipeline_OracleFail_ThenTaskComplete_Rejected()
    {
        var (registry, llm, _) = CreatePipeline();
        llm.NextResponse = OracleFail();

        var verifyResult = await InvokeVerifyTask(registry);
        Assert.Equal("fail", GetStatus(verifyResult));

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("error", GetStatus(updateResult));
    }

    [Fact]
    public async Task TaskComplete_WithoutVerification_Rejected()
    {
        var (registry, _, _) = CreatePipeline();

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("error", GetStatus(updateResult));
    }

    [Fact]
    public async Task FullPipeline_ComponentScope_OraclePass_ThenComplete_Accepted()
    {
        var (registry, llm, tracker) = CreatePipeline();
        llm.NextResponse = OraclePass();

        var verifyResult = await InvokeVerifyComponent(registry);
        Assert.Equal("success", GetStatus(verifyResult));
        Assert.True(tracker.IsVerified(VerificationScope.Component, "c1"));
    }

    [Fact]
    public async Task FullPipeline_ModuleScope_OraclePass_ThenComplete_Accepted()
    {
        var (registry, llm, tracker) = CreatePipeline();
        llm.NextResponse = OraclePass();

        var verifyResult = await InvokeVerifyModule(registry);
        Assert.Equal("success", GetStatus(verifyResult));
        Assert.True(tracker.IsVerified(VerificationScope.Module, "m1"));
    }

    [Fact]
    public async Task FullPipeline_OracleMalformedJson_ThenTaskComplete_Rejected()
    {
        var (registry, llm, _) = CreatePipeline();
        llm.NextResponse = OracleMalformed();

        var verifyResult = await InvokeVerifyTask(registry);
        Assert.Equal("fail", GetStatus(verifyResult));

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("error", GetStatus(updateResult));
    }

    [Fact]
    public async Task FullPipeline_ResetClearsVerification_ThenRejected()
    {
        var (registry, llm, tracker) = CreatePipeline();
        llm.NextResponse = OraclePass();

        var verifyResult = await InvokeVerifyTask(registry);
        Assert.Equal("success", GetStatus(verifyResult));

        tracker.ResetForInvocation();

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("error", GetStatus(updateResult));
    }

    [Fact]
    public async Task FullPipeline_OraclePassForTask_DoesNotAllowDifferentTask()
    {
        var (registry, llm, _) = CreatePipeline();
        llm.NextResponse = OraclePass();

        var verifyResult = await InvokeVerifyTask(registry, taskId: "task-A");
        Assert.Equal("success", GetStatus(verifyResult));

        var updateResult = await InvokeUpdateTaskStatus(registry, taskId: "task-B");
        Assert.Equal("error", GetStatus(updateResult));
    }

    [Fact]
    public async Task FullPipeline_OraclePass_ThenRetryVerification_OracleFail_ThenTaskComplete_Rejected()
    {
        var (registry, llm, _) = CreatePipeline();

        // First verify passes
        llm.NextResponse = OraclePass();
        var firstVerify = await InvokeVerifyTask(registry);
        Assert.Equal("success", GetStatus(firstVerify));

        // Second verify fails (last-write-wins)
        llm.NextResponse = OracleFail();
        var secondVerify = await InvokeVerifyTask(registry);
        Assert.Equal("fail", GetStatus(secondVerify));

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("error", GetStatus(updateResult));
    }

    [Fact]
    public async Task FullPipeline_OracleFail_ThenRetryVerification_OraclePass_ThenTaskComplete_Accepted()
    {
        var (registry, llm, _) = CreatePipeline();

        // First verify fails
        llm.NextResponse = OracleFail();
        var firstVerify = await InvokeVerifyTask(registry);
        Assert.Equal("fail", GetStatus(firstVerify));

        // Second verify passes (last-write-wins)
        llm.NextResponse = OraclePass();
        var secondVerify = await InvokeVerifyTask(registry);
        Assert.Equal("success", GetStatus(secondVerify));

        var updateResult = await InvokeUpdateTaskStatus(registry);
        Assert.Equal("success", GetStatus(updateResult));
    }

    #region Inner Types

    private sealed class FakeLlmService : ILlmService
    {
        public string NextResponse { get; set; } = "";

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt, string model, IReadOnlyList<LopenToolDefinition> tools, CancellationToken ct)
        {
            return Task.FromResult(new LlmInvocationResult(
                Output: NextResponse,
                TokenUsage: new TokenUsage(100, 50, 150, 8192, false),
                ToolCallsMade: 0,
                IsComplete: true));
        }
    }

    private sealed class TrackingToolRegistry : IToolRegistry
    {
        public Dictionary<string, Func<string, CancellationToken, Task<string>>> BoundHandlers { get; } = [];
        public void RegisterTool(LopenToolDefinition tool) { }
        public IReadOnlyList<LopenToolDefinition> GetAllTools() => [];
        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase) => [];

        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler)
        {
            BoundHandlers[toolName] = handler;
            return true;
        }
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult("");
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string sourcePath, string destinationPath) { }
        public void DeleteFile(string path) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public void DeleteDirectory(string path, bool recursive = true) { }
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.MinValue;
    }

    private sealed class StubSectionExtractor : ISectionExtractor
    {
        public IReadOnlyList<ExtractedSection> ExtractRelevantSections(string specContent, IReadOnlyList<string> relevantHeaders) => [];
        public IReadOnlyList<ExtractedSection> ExtractAllSections(string specContent) => [];
    }

    private sealed class StubWorkflowEngine : IWorkflowEngine
    {
        public WorkflowStep CurrentStep => WorkflowStep.IterateThroughTasks;
        public bool IsComplete => false;
        public WorkflowPhase CurrentPhase => WorkflowPhase.Building;
        public Task InitializeAsync(string moduleName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool Fire(WorkflowTrigger trigger) => true;
        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() => [];
    }

    #endregion
}
