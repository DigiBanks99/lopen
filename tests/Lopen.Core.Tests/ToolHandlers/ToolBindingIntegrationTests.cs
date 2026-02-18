using System.Text.Json;
using Lopen.Core.Documents;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.ToolHandlers;

public class ToolBindingIntegrationTests
{
    [Fact]
    public void BindAll_BindsHandlersToRegistry_AndToolsHaveNonNullHandlers()
    {
        // Create a registry that stores tools and supports handler binding
        var registry = new StoringToolRegistry();

        // Register the 10 built-in tools that ToolHandlerBinder expects
        var toolNames = new[]
        {
            "read_spec", "read_research", "read_plan", "update_task_status",
            "get_current_context", "log_research", "report_progress",
            "verify_task_completion", "verify_component_completion", "verify_module_completion"
        };
        foreach (var name in toolNames)
            registry.RegisterTool(new LopenToolDefinition(name, $"Description for {name}"));

        // Create binder with minimal stubs
        var binder = new ToolHandlerBinder(
            new StubFileSystem(),
            new StubSectionExtractor(),
            new StubWorkflowEngine(),
            new StubVerificationTracker(),
            NullLogger<ToolHandlerBinder>.Instance,
            "/test/project");

        // Act
        binder.BindAll(registry);

        // Assert: all tools should now have non-null handlers
        var tools = registry.GetAllTools();
        Assert.Equal(10, tools.Count);
        foreach (var tool in tools)
        {
            Assert.NotNull(tool.Handler);
        }
    }

    [Fact]
    public async Task ReadSpec_WhenFileExists_ReturnsSpecContent()
    {
        var fs = new StubFileSystem();
        var specPath = Path.Combine("/test/project", "docs", "requirements", "core", "SPECIFICATION.md");
        fs.Files[specPath] = "# Core Specification\nThis is the spec content.";

        var registry = CreateBoundRegistry(fs);
        var handler = GetHandler(registry, "read_spec");

        var result = await handler("""{"module":"core"}""", CancellationToken.None);

        Assert.Contains("Core Specification", result);
        Assert.Contains("spec content", result);
    }

    [Fact]
    public async Task ReadResearch_WhenFileExists_ReturnsResearchContent()
    {
        var fs = new StubFileSystem();
        var researchDir = Path.Combine("/test/project", "docs", "requirements", "llm");
        fs.Directories.Add(researchDir);
        var researchPath = Path.Combine(researchDir, "RESEARCH.md");
        fs.Files[researchPath] = "# LLM Research\nFindings about LLM integration.";

        var registry = CreateBoundRegistry(fs);
        var handler = GetHandler(registry, "read_research");

        var result = await handler("""{"module":"llm"}""", CancellationToken.None);

        Assert.Contains("LLM Research", result);
        Assert.Contains("Findings about LLM integration", result);
    }

    [Fact]
    public async Task ReadPlan_WhenFileExists_ReturnsPlanContent()
    {
        var fs = new StubFileSystem();
        var planPath = Path.Combine("/test/project", "docs", "requirements", "IMPLEMENTATION_PLAN.md");
        fs.Files[planPath] = "# Implementation Plan\n- [ ] Task 1\n- [ ] Task 2";

        var registry = CreateBoundRegistry(fs);
        var handler = GetHandler(registry, "read_plan");

        var result = await handler("{}", CancellationToken.None);

        Assert.Contains("Implementation Plan", result);
        Assert.Contains("Task 1", result);
    }

    [Fact]
    public async Task UpdateTaskStatus_NonComplete_ReturnsSuccess()
    {
        var registry = CreateBoundRegistry();
        var handler = GetHandler(registry, "update_task_status");

        var result = await handler("""{"task_id":"T-01","status":"in_progress"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Contains("T-01", result);
        Assert.Contains("in_progress", result);
    }

    [Fact]
    public async Task GetCurrentContext_ReturnsWorkflowState()
    {
        var registry = CreateBoundRegistry();
        var handler = GetHandler(registry, "get_current_context");

        var result = await handler("{}", CancellationToken.None);

        var json = JsonDocument.Parse(result);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("step", out _));
        Assert.True(root.TryGetProperty("phase", out _));
        Assert.True(root.TryGetProperty("is_complete", out _));
        Assert.True(root.TryGetProperty("permitted_triggers", out _));
    }

    [Fact]
    public async Task LogResearch_WritesFileAndReturnsSuccess()
    {
        var fs = new StubFileSystem();
        var registry = CreateBoundRegistry(fs);
        var handler = GetHandler(registry, "log_research");

        var result = await handler(
            """{"module":"core","topic":"caching","content":"# Caching Research\nDetails here"}""",
            CancellationToken.None);

        Assert.Contains("success", result);

        var expectedPath = Path.Combine("/test/project", "docs", "requirements", "core", "RESEARCH-caching.md");
        Assert.True(fs.Files.ContainsKey(expectedPath), $"Expected file at {expectedPath}");
        Assert.Contains("Caching Research", fs.Files[expectedPath]);
    }

    [Fact]
    public async Task ReportProgress_ReturnsSuccess()
    {
        var registry = CreateBoundRegistry();
        var handler = GetHandler(registry, "report_progress");

        var result = await handler("""{"summary":"Phase 1 complete"}""", CancellationToken.None);

        Assert.Contains("success", result);
        Assert.Contains("Phase 1 complete", result);
    }

    // --- Helpers ---

    private static StoringToolRegistry CreateBoundRegistry(StubFileSystem? fs = null)
    {
        var registry = new StoringToolRegistry();
        var toolNames = new[]
        {
            "read_spec", "read_research", "read_plan", "update_task_status",
            "get_current_context", "log_research", "report_progress",
            "verify_task_completion", "verify_component_completion", "verify_module_completion"
        };
        foreach (var name in toolNames)
            registry.RegisterTool(new LopenToolDefinition(name, $"Description for {name}"));

        var binder = new ToolHandlerBinder(
            fs ?? new StubFileSystem(),
            new StubSectionExtractor(),
            new StubWorkflowEngine(),
            new StubVerificationTracker(),
            NullLogger<ToolHandlerBinder>.Instance,
            "/test/project");

        binder.BindAll(registry);
        return registry;
    }

    private static Func<string, CancellationToken, Task<string>> GetHandler(
        StoringToolRegistry registry, string toolName)
    {
        var tool = registry.GetAllTools().First(t => t.Name == toolName);
        Assert.NotNull(tool.Handler);
        return tool.Handler;
    }

    // --- Stubs ---

    private sealed class StoringToolRegistry : IToolRegistry
    {
        private readonly List<LopenToolDefinition> _tools = [];

        public void RegisterTool(LopenToolDefinition tool) => _tools.Add(tool);

        public IReadOnlyList<LopenToolDefinition> GetToolsForPhase(WorkflowPhase phase) =>
            _tools.Where(t => t.AvailableInPhases is null || t.AvailableInPhases.Contains(phase)).ToList().AsReadOnly();

        public IReadOnlyList<LopenToolDefinition> GetAllTools() => _tools.AsReadOnly();

        public bool BindHandler(string toolName, Func<string, CancellationToken, Task<string>> handler)
        {
            var index = _tools.FindIndex(t => t.Name == toolName);
            if (index < 0)
                return false;
            _tools[index] = _tools[index] with { Handler = handler };
            return true;
        }
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public Dictionary<string, string> Files { get; } = new();
        public HashSet<string> Directories { get; } = new();

        public void CreateDirectory(string path) => Directories.Add(path);
        public bool FileExists(string path) => Files.ContainsKey(path);
        public bool DirectoryExists(string path) => Directories.Contains(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => Task.FromResult(Files[path]);

        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
        {
            Files[path] = content;
            return Task.CompletedTask;
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern = "*")
        {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern)
                .Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Files.Keys
                .Where(f => f.StartsWith(path + Path.DirectorySeparatorChar) || f.StartsWith(path + "/"))
                .Where(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), pattern));
        }

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
        public IReadOnlyList<ExtractedSection> ExtractRelevantSections(string specContent, IReadOnlyList<string> relevantHeaders) => [];
        public IReadOnlyList<ExtractedSection> ExtractAllSections(string specContent) => [];
    }

    private sealed class StubWorkflowEngine : IWorkflowEngine
    {
        public WorkflowStep CurrentStep => WorkflowStep.DraftSpecification;
        public WorkflowPhase CurrentPhase => WorkflowPhase.RequirementGathering;
        public bool IsComplete => false;

        public Task InitializeAsync(string moduleName, CancellationToken ct = default) => Task.CompletedTask;
        public bool Fire(WorkflowTrigger trigger) => true;
        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() => [WorkflowTrigger.Assess];
    }

    private sealed class StubVerificationTracker : IVerificationTracker
    {
        public void RecordVerification(VerificationScope scope, string identifier, bool passed) { }
        public bool IsVerified(VerificationScope scope, string identifier) => false;
        public void ResetForInvocation() { }
    }
}
