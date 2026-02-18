using Lopen.Core.Documents;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Lopen.Llm;
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

    private sealed class StubFileSystem : Lopen.Storage.IFileSystem
    {
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => Task.FromResult("");
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default) => Task.CompletedTask;
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string source, string dest) { }
        public void DeleteFile(string path) { }
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
